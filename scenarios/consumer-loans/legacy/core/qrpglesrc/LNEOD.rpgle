**FREE
// LNEOD - consumer loan end-of-day (job LNEOD in the nightly batch, after the
// payment posting run). For every active loan:
//   - due date reached and not covered by payments -> instalment to arrears, event MISS
//   - instalment due today                         -> event DUE
//   - arrears > 0                                  -> days past due recalculated, event DPD
// Loans 90+ DPD move to status COLL. Events go to data queue LNLIB/LNEVTQ
// (64 byte entries, layout CoreLayouts.EventEntry) for the lending collections service.
ctl-opt dftactgrp(*no) actgrp(*new) option(*srcstmt) datfmt(*iso) commit(*chg);

dcl-f LNMSTP keyed usage(*update);
dcl-f BUSDATE usage(*update);

dcl-ds event qualified;
  type    char(4);
  account char(12);
  date    zoned(8:0);
  time    zoned(6:0);
  amount  packed(13:2);
  arrears packed(13:2);
  dpd     zoned(4:0);
  filler  char(16);
end-ds;

dcl-pr QSNDDTAQ extpgm('QSNDDTAQ');
  queue  char(10) const;
  lib    char(10) const;
  length packed(5:0) const;
  data   char(64) const;
end-pr;
dcl-pr nextDue zoned(8:0) extproc('LNCALC_NEXTDUE');
  current zoned(8:0) const;
  dueDay  packed(3:0) const;
end-pr;

dcl-s today date;
dcl-s todayNum zoned(8:0);
dcl-s dueToday packed(5:0);
dcl-s missed packed(5:0);

read BUSDATE;
BDPREV = BDDATE;
BDDATE += %days(1);
today = BDDATE;
todayNum = %dec(today:*iso);

setll *loval LNMSTP;
read LNMSTP;
dow not %eof(LNMSTP);
  if LMSTAT = 'ACTV' or LMSTAT = 'COLL';
    if LMNXDT = todayNum;
      dueToday += 1;
      send('DUE ':LMNXAM);
    elseif LMNXDT < todayNum and LMNXAM > 0;
      // the payment run clears LMNXAM when the instalment was paid
      missed += 1;
      LMARRS += LMNXAM;
      if LMOLDU = 0;
        LMOLDU = LMNXDT;
      endif;
      send('MISS':LMNXAM);
      LMNXDT = nextDue(LMNXDT:LMDUED);
    endif;
    if LMARRS > 0 and LMOLDU > 0;
      LMDPD = %diff(today:%date(LMOLDU:*iso):*days);
      if LMDPD >= 90 and LMSTAT = 'ACTV';
        LMSTAT = 'COLL';
      endif;
      send('DPD ':0);
    endif;
    LMCHGT = %timestamp();
    update LNMSTR;
  endif;
  read LNMSTP;
enddo;

update BUSDATR;
commit;
dsply ('LNEOD ' + %char(today) + ' due ' + %char(dueToday) + ' missed ' + %char(missed));
*inlr = *on;
return;

dcl-proc send;
  dcl-pi *n;
    type   char(4) const;
    amount packed(13:2) const;
  end-pi;
  clear event;
  event.type = type;
  event.account = LMACCT;
  event.date = todayNum;
  event.time = %dec(%time():*hms);
  event.amount = amount;
  event.arrears = LMARRS;
  event.dpd = LMDPD;
  QSNDDTAQ('LNEVTQ':'LNLIB':64:event);
end-proc;
