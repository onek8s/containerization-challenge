/*
 * Meridian Rating Systems RateEngine 7.2 SDK
 * MRSRATE32.DLL public interface
 *
 * Copyright (c) Meridian Rating Systems, Inc. All rights reserved.
 * Licensed for use by Contoso Freight Brokerage under agreement MRS-2019-0417.
 */
#ifndef MRSRATE_H
#define MRSRATE_H

#include <windows.h>

#ifdef __cplusplus
extern "C" {
#endif

#define MRS_API __declspec(dllimport) LONG WINAPI

#pragma pack(push, 1)

#define MR_MAX_LINES      20
#define MR_ZIP_LEN        6
#define MR_COUNTRY_LEN    4
#define MR_TARIFF_LEN     13
#define MR_TERMINAL_LEN   8

/* Header flags */
#define MR_FLAG_NONE            0x0000
#define MR_FLAG_SINGLE_SHIPMENT 0x0001
#define MR_FLAG_DEFICIT_RATING  0x0002
#define MR_FLAG_TRANSIT_DAYS    0x0004

typedef struct _MR_SHIPMENT_HEADER {
    CHAR  OriginZip[MR_ZIP_LEN];
    CHAR  DestZip[MR_ZIP_LEN];
    CHAR  OriginCountry[MR_COUNTRY_LEN];   /* "USA", "CAN" */
    CHAR  DestCountry[MR_COUNTRY_LEN];
    LONG  ShipDate;                        /* yyyymmdd */
    CHAR  TariffName[MR_TARIFF_LEN];       /* empty = session default */
    LONG  Flags;
} MR_SHIPMENT_HEADER;

typedef struct _MR_LINE_ITEM {
    LONG  ClassX10;                        /* 500..5000 */
    LONG  WeightLbs;
} MR_LINE_ITEM;

typedef struct _MR_LINE_RESULT {
    LONG  RatePerCwtCents;
    LONG  ChargeCents;
} MR_LINE_RESULT;

typedef struct _MR_RATE_RESULT {
    LONG  Status;
    LONG  TotalCents;
    LONG  MinimumChargeCents;
    LONG  DeficitWeightLbs;
    LONG  DeficitChargeCents;
    LONG  TransitDays;
    LONG  LineCount;
    MR_LINE_RESULT Lines[MR_MAX_LINES];
    CHAR  TariffUsed[MR_TARIFF_LEN];
    CHAR  OriginTerminal[MR_TERMINAL_LEN];
    CHAR  DestTerminal[MR_TERMINAL_LEN];
} MR_RATE_RESULT;

typedef struct _MR_KEY_STATUS {
    LONG  Present;
    CHAR  Serial[16];
    LONG  LicenceDaysLeft;
    LONG  Seats;
    LONG  SeatsInUse;
} MR_KEY_STATUS;

#pragma pack(pop)

typedef LONG MR_HSESSION;

/* Return codes */
#define MR_OK                     0
#define MR_E_NOT_INITIALIZED   (-1)
#define MR_E_BAD_HANDLE        (-2)
#define MR_E_WRONG_THREAD      (-3)
#define MR_E_NOKEY           (-101)   /* licence key not attached */
#define MR_E_KEY_MISMATCH    (-102)   /* licence file issued for another key */
#define MR_E_LICENCE_EXPIRED (-103)
#define MR_E_NO_SEATS        (-104)
#define MR_E_BAD_PASSWORD    (-105)
#define MR_E_DATA_PATH       (-201)
#define MR_E_TARIFF_MISSING  (-202)
#define MR_E_TARIFF_EXPIRED  (-203)
#define MR_E_BAD_ORIGIN      (-301)
#define MR_E_BAD_DEST        (-302)
#define MR_E_POINT_NOT_SERVED (-303)
#define MR_E_BAD_CLASS       (-304)
#define MR_E_BAD_WEIGHT      (-305)
#define MR_E_TOO_MANY_LINES  (-306)

/*
 * Sessions are bound to the thread that opened them. All calls for a session
 * must be made on that thread.
 */
MRS_API MR_Initialize(LPCSTR dataPath, LPCSTR licenceFile, LPCSTR password, MR_HSESSION *session);
MRS_API MR_Terminate(MR_HSESSION session);
MRS_API MR_RateShipment(MR_HSESSION session, const MR_SHIPMENT_HEADER *header,
                        const MR_LINE_ITEM *lines, LONG lineCount, MR_RATE_RESULT *result);
MRS_API MR_GetTariffInfo(MR_HSESSION session, LPSTR tariffName, LONG tariffNameLen, LONG *effectiveDate);
MRS_API MR_ReloadTariffs(MR_HSESSION session);
MRS_API MR_GetKeyStatus(MR_KEY_STATUS *status);
MRS_API MR_GetVersion(LPSTR version, LONG versionLen);
MRS_API MR_GetLastErrorText(MR_HSESSION session, LPSTR text, LONG textLen);

#ifdef __cplusplus
}
#endif

#endif /* MRSRATE_H */
