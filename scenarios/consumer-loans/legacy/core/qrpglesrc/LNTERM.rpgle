**FREE
// LNTERM - terminate (accelerate) a loan after the final demand; whole balance becomes due
// Parameters: IN 30 bytes, OUT 80 bytes (CoreLayouts.LnTermIn/ResultOut)
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso) commit(*chg);

dcl-f LNMSTP keyed usage(*update);

dcl-ds inParm qualified;
  account char(12);
  effdate zoned(8:0);
  officer char(10);
end-ds;
dcl-ds result qualified;
  rc  char(2);
  msg char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(result);
end-pi;

clear pOut;
chain (pIn.account) LNMSTP;
if not %found(LNMSTP);
  pOut.rc = 'NF';
  pOut.msg = 'Loan not found';
  return;
endif;
if LMSTAT = 'TERM' or LMSTAT = 'CLSD';
  unlock LNMSTP;
  pOut.rc = 'DU';
  pOut.msg = 'Loan already terminated or closed';
  return;
endif;

LMARRS += LMBALA;
LMNXDT = pIn.effdate;
LMNXAM = LMARRS;
LMSTAT = 'TERM';
LMOFCR = pIn.officer;
LMCHGT = %timestamp();
update LNMSTR;
commit;
pOut.rc = '00';
*inlr = *on;
return;
