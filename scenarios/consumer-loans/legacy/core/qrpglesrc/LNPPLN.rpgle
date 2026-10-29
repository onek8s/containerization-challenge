**FREE
// LNPPLN - register a payment plan (up to 12 instalments) for a delinquent loan;
// LNEOD then expects the plan amounts instead of the regular instalment.
// Parameters: IN 192 bytes, OUT 80 bytes (CoreLayouts.LnPplnIn/ResultOut)
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso) commit(*chg);

dcl-f LNMSTP keyed usage(*input);
dcl-f LNPPLP keyed usage(*update:*output:*delete);

dcl-ds instalment qualified template;
  due    zoned(8:0);
  amount packed(11:2);
end-ds;

dcl-ds inParm qualified;
  account char(12);
  officer char(10);
  count   packed(3:0);
  line    likeds(instalment) dim(12);
end-ds;
dcl-ds result qualified;
  rc  char(2);
  msg char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(result);
end-pi;

dcl-s i int(5);

clear pOut;
chain (pIn.account) LNMSTP;
if not %found(LNMSTP);
  pOut.rc = 'NF';
  pOut.msg = 'Loan not found';
  return;
endif;
if pIn.count < 1 or pIn.count > 12;
  pOut.rc = 'RJ';
  pOut.msg = 'Plan must have 1-12 instalments';
  return;
endif;

// replace any previous plan
setll (pIn.account) LNPPLP;
reade (pIn.account) LNPPLP;
dow not %eof(LNPPLP);
  delete LNPPLR;
  reade (pIn.account) LNPPLP;
enddo;

for i = 1 to pIn.count;
  PPACCT = pIn.account;
  PPNUMB = i;
  PPDUDT = pIn.line(i).due;
  PPAMNT = pIn.line(i).amount;
  PPSTAT = 'O';
  PPOFCR = pIn.officer;
  write LNPPLR;
endfor;
commit;
pOut.rc = '00';
*inlr = *on;
return;
