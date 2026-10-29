**FREE
// LNFEE - charge a fee (reminder, plan) to a loan; added to arrears
// Parameters: IN 61 bytes, OUT 80 bytes (CoreLayouts.LnFeeIn/ResultOut)
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso) commit(*chg);

dcl-f LNMSTP keyed usage(*update);
dcl-f LNTRNP usage(*output);

dcl-ds inParm qualified;
  account char(12);
  feecode char(4);
  amount  packed(9:2);
  text    char(40);
end-ds;
dcl-ds result qualified;
  rc  char(2);
  msg char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(result);
end-pi;

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
if LMSTAT = 'CLSD' or pIn.amount <= 0 or pIn.amount > 100;
  unlock LNMSTP;
  pOut.rc = 'RJ';
  pOut.msg = 'Fee not allowed';
  return;
endif;

LMARRS += pIn.amount;
LMCHGT = %timestamp();
update LNMSTR;

clear LNTRNR;
TRACCT = pIn.account;
TRSEQN = nextNumber('LNTRN');
TRBKDT = %dec(%date():*iso);
TRVLDT = TRBKDT;
TRTYPE = 'Fee ' + pIn.feecode;
TRAMT  = pIn.amount;
TRFEES = pIn.amount;
TRBALA = LMBALA;
TRRVSD = 'N';
write LNTRNR;
commit;
pOut.rc = '00';
*inlr = *on;
return;
