**FREE
// LNDISB - pay out a booked loan: own-bank IBAN is credited directly,
// other banks get an outgoing SEPA credit transfer via PAYLIB/SCTOUT.
// Parameters: IN 133 bytes, OUT 101 bytes (CoreLayouts.LnDisbIn/Out)
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso) commit(*chg);

dcl-f LNMSTP keyed usage(*update);
dcl-f LNTRNP usage(*output);

dcl-ds inParm qualified;
  account char(12);
  amount  packed(13:2);
  iban    char(34);
  text    char(70);
  officer char(10);
end-ds;

dcl-ds outParm qualified;
  rc       char(2);
  txnid    char(20);
  external char(1);
  msg      char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(outParm);
end-pi;

dcl-pr postCredit char(20) extpgm('DPLIB/DPPOSTCR');
  iban   char(34) const;
  amount packed(13:2) const;
  text   char(70) const;
end-pr;
dcl-pr sendSct char(20) extpgm('PAYLIB/SCTOUT');
  iban   char(34) const;
  amount packed(13:2) const;
  text   char(70) const;
  debtor char(12) const;
end-pr;
dcl-pr nextNumber packed(10:0) extpgm('LNNXTNBR');
  counter char(10) const;
end-pr;

clear pOut;
chain (pIn.account) LNMSTP;
if not %found(LNMSTP);
  pOut.rc = 'NF';
  pOut.msg = 'Loan not found';
  return;
endif;
if LMSTAT <> 'BOOK';
  unlock LNMSTP;
  pOut.rc = 'DU';
  pOut.msg = 'Loan already disbursed';
  return;
endif;
if pIn.amount <> LMORIG;
  unlock LNMSTP;
  pOut.rc = 'RJ';
  pOut.msg = 'Amount differs from booked amount';
  return;
endif;

if %subst(pIn.iban:1:2) = 'FI' and %subst(pIn.iban:5:4) = '4790';
  pOut.txnid = postCredit(pIn.iban:pIn.amount:pIn.text);
  pOut.external = 'N';
else;
  pOut.txnid = sendSct(pIn.iban:pIn.amount:pIn.text:pIn.account);
  pOut.external = 'Y';
endif;

clear LNTRNR;
TRACCT = pIn.account;
TRSEQN = nextNumber('LNTRN');
TRBKDT = %dec(%date():*iso);
TRVLDT = TRBKDT;
TRTYPE = 'Disbursement';
TRAMT  = pIn.amount;
TRPRIN = pIn.amount;
TRBALA = pIn.amount;
TRTXID = pOut.txnid;
TRRVSD = 'N';
write LNTRNR;

LMSTAT = 'ACTV';
LMCHGT = %timestamp();
update LNMSTR;
commit;
pOut.rc = '00';
*inlr = *on;
return;
