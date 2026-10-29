**FREE
// CUSCRT - create a prospect customer (CIF) for a new borrower
// Parameters: IN 242 bytes, OUT 90 bytes (CoreLayouts.CusCrtIn/Out)
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso);

dcl-f CIFMSTL1 keyed usage(*input) extdesc('CIFLIB/CIFMSTL1') extfile(*extdesc) rename(CIFMSTR:CIF1);
dcl-f CIFMSTP keyed usage(*input:*output) extdesc('CIFLIB/CIFMSTP') extfile(*extdesc);

dcl-ds inParm qualified;
  hetu    char(11);
  fname   char(30);
  lname   char(30);
  street  char(40);
  postal  char(5);
  city    char(30);
  citizen char(2);
  email   char(60);
  phone   char(20);
  branch  char(4);
  officer char(10);
end-ds;

dcl-ds outParm qualified;
  rc  char(2);
  cif char(10);
  msg char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(outParm);
end-pi;

dcl-pr nextNumber packed(10:0) extpgm('CIFLIB/CIFNXTNBR');
  counter char(10) const;
end-pr;

clear pOut;
chain (%upper(pIn.hetu)) CIF1;
if %found(CIFMSTL1);
  pOut.rc = 'DU';
  pOut.cif = CMCIF;
  pOut.msg = 'Customer exists';
  *inlr = *on;
  return;
endif;

clear CIFMSTR;
CMCIF  = %editc(nextNumber('CIF'):'X');
CMHETU = %upper(pIn.hetu);
CMFNAM = %upper(pIn.fname);
CMLNAM = %upper(pIn.lname);
CMSTRT = pIn.street;
CMPOST = pIn.postal;
CMCITY = %upper(pIn.city);
CMCTZN = pIn.citizen;
CMEMAL = pIn.email;
CMPHON = pIn.phone;
CMSEGM = 'PRI';
CMKYC  = 'P';
CMSNCE = %dec(%date():*iso);
CMRATG = '  ';
CMDECD = 'N';
CMBLCK = 'N';
CMBRCH = pIn.branch;
CMOFCR = pIn.officer;
monitor;
  write CIFMSTR;
  pOut.rc = '00';
  pOut.cif = CMCIF;
on-error 1021;
  pOut.rc = 'DU';
  pOut.msg = 'Duplicate CIF';
on-error;
  pOut.rc = 'SE';
  pOut.msg = 'CIFMSTP write failed';
endmon;
*inlr = *on;
return;
