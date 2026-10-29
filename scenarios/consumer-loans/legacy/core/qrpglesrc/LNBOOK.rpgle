**FREE
// LNBOOK - open a consumer loan account (status BOOK until LNDISB pays it out)
// Parameters: IN 97 bytes, OUT 92 bytes (CoreLayouts.LnBookIn/Out)
// Idempotent on the application reference: a second call returns DU and the existing account.
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso) commit(*chg);

dcl-f LNMSTP keyed usage(*input:*output);
dcl-f LNMSTL2 keyed usage(*input) rename(LNMSTR:LNMST2) prefix(L2_);   // by LMAPRF
dcl-f CIFMSTP keyed usage(*input) extdesc('CIFLIB/CIFMSTP') extfile(*extdesc);

dcl-ds inParm qualified;
  appref   char(20);
  cif      char(10);
  cocif    char(10);
  product  char(4);
  amount   packed(13:2);
  term     packed(3:0);
  refrate  char(3);
  margin   packed(7:4);
  rate     packed(7:4);
  arrfee   packed(9:2);
  mthfee   packed(7:2);
  dueday   packed(3:0);
  firstdue zoned(8:0);
  branch   char(4);
  officer  char(10);
end-ds;

dcl-ds outParm qualified;
  rc      char(2);
  account char(12);
  msg     char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(outParm);
end-pi;

dcl-pr nextNumber packed(10:0) extpgm('LNNXTNBR');
  counter char(10) const;
end-pr;
dcl-pr annuity packed(11:2) extproc('LNCALC_ANNUITY');
  principal packed(13:2) const;
  ratePct   packed(7:4) const;
  months    packed(3:0) const;
end-pr;

clear pOut;
chain (pIn.appref) LNMST2;
if %found(LNMSTL2);
  pOut.rc = 'DU';
  pOut.account = L2_LMACCT;
  pOut.msg = 'Application already booked';
  return;
endif;

chain (pIn.cif) CIFMSTP;
if not %found(CIFMSTP) or CMBLCK = 'Y' or CMDECD = 'Y';
  pOut.rc = 'RJ';
  pOut.msg = 'Borrower not found or blocked';
  return;
endif;
if pIn.amount <= 0 or pIn.term < 6 or pIn.term > 180 or pIn.dueday < 1 or pIn.dueday > 28;
  pOut.rc = 'RJ';
  pOut.msg = 'Invalid amount, term or due day';
  return;
endif;

clear LNMSTR;
LMACCT = '47908510' + %subst(%editc(nextNumber('LOAN'):'X'):7:4);
LMCIF  = pIn.cif;
LMCOCF = pIn.cocif;
LMAPRF = pIn.appref;
LMPROD = pIn.product;
LMORIG = pIn.amount;
LMBALA = pIn.amount;
LMREFR = pIn.refrate;
LMMARG = pIn.margin;
LMRATE = pIn.rate;
LMAFEE = pIn.arrfee;
LMMFEE = pIn.mthfee;
LMTERM = pIn.term;
LMDUED = pIn.dueday;
LMNXDT = pIn.firstdue;
LMNXAM = annuity(pIn.amount:pIn.rate:pIn.term) + pIn.mthfee;
LMSTAT = 'BOOK';
LMOPDT = %dec(%date():*iso);
LMBRCH = pIn.branch;
LMOFCR = pIn.officer;
LMCHGT = %timestamp();
write LNMSTR;
commit;

pOut.rc = '00';
pOut.account = LMACCT;
*inlr = *on;
return;
