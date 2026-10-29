**FREE
// CUSTINQ - customer inquiry by personal identity code or CIF
// Parameters: IN 21 bytes, OUT 256 bytes (see CoreLayouts.CustInqIn/Out)
ctl-opt dftactgrp(*no) actgrp('LENDING') option(*srcstmt) datfmt(*iso);

dcl-f CIFMSTL1 keyed usage(*input) prefix(C1_) extdesc('CIFLIB/CIFMSTL1') extfile(*extdesc) rename(CIFMSTR:CIF1);
dcl-f CIFMSTP keyed usage(*input) prefix(C_) extdesc('CIFLIB/CIFMSTP') extfile(*extdesc);

dcl-ds inParm qualified;
  hetu char(11);
  cif  char(10);
end-ds;

dcl-ds outParm qualified;
  rc       char(2);
  cif      char(10);
  hetu     char(11);
  name     char(60);
  street   char(40);
  postal   char(5);
  city     char(30);
  segment  char(3);
  kyc      char(1);
  since    zoned(8:0);
  rating   char(2);
  deceased char(1);
  blocked  char(1);
  branch   char(4);
  msg      char(78);
end-ds;

dcl-pi *n;
  pIn  likeds(inParm);
  pOut likeds(outParm);
end-pi;

clear pOut;
pOut.rc = 'NF';

if pIn.cif <> *blanks;
  chain (pIn.cif) CIFMSTP;
  if %found(CIFMSTP);
    fill();
  endif;
elseif pIn.hetu <> *blanks;
  chain (%upper(pIn.hetu)) CIF1;
  if %found(CIFMSTL1);
    chain (C1_CMCIF) CIFMSTP;
    if %found(CIFMSTP);
      fill();
    endif;
  endif;
endif;

if pOut.rc = 'NF';
  pOut.msg = 'Customer not found';
endif;
*inlr = *on;
return;

dcl-proc fill;
  pOut.rc       = '00';
  pOut.cif      = C_CMCIF;
  pOut.hetu     = C_CMHETU;
  pOut.name     = %trim(C_CMLNAM) + ' ' + %trim(C_CMFNAM);
  pOut.street   = C_CMSTRT;
  pOut.postal   = C_CMPOST;
  pOut.city     = C_CMCITY;
  pOut.segment  = C_CMSEGM;
  pOut.kyc      = C_CMKYC;
  pOut.since    = C_CMSNCE;
  pOut.rating   = C_CMRATG;
  pOut.deceased = C_CMDECD;
  pOut.blocked  = C_CMBLCK;
  pOut.branch   = C_CMBRCH;
end-proc;
