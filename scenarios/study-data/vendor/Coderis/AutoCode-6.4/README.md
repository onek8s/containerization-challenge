# Coderis AutoCode 6.4

Licensed autocoder for MedDRA and WHODrug. The installer, engine, licence and dictionary files are
supplied by Coderis to licensed customers and are not stored here. This folder documents the parts of
the type library (`CoderisAC6.tlb`) that Study Data uses.

| Item | Value |
| --- | --- |
| Engine | `C:\Program Files (x86)\Coderis\AutoCode 6\CoderisAC6.dll` (32-bit, in-process COM server, apartment threaded) |
| ProgID | `Coderis.AutoCoder.6` |
| CLSID | `{8B1F3C27-5D4E-4A61-9E0B-2C7D41A9F6E3}` |
| Interface | `IAutoCoder` `{4E9A2B71-0C3D-4F58-A6E1-93B7D2C05A18}`, dispatch only |
| Licence | `licence\studydata.lic`, issued per server (host name and MAC address) |
| Dictionaries | `dictionaries\MEDDRA\<version>`, `dictionaries\WHODRUG\<version>` imported with the Coderis Dictionary Manager |

## IAutoCoder

| DispId | Member | Notes |
| --- | --- | --- |
| 1 | `void OpenDictionary(BSTR dictionary, BSTR version, BSTR licenceFile)` | `MEDDRA` or `WHODRUG`; loads the dictionary into memory (about 20 s for MedDRA) |
| 2 | `BSTR DictionaryInfo(BSTR dictionary)` | for example `MedDRA 27.1 English` |
| 3 | `VARIANT Code(BSTR dictionary, BSTR verbatim, long maxCandidates, long flags)` | SAFEARRAY of BSTR, one candidate per element: `code \t term \t preferred code \t preferred term \t hierarchy \t score`; `VT_EMPTY` when nothing matches |
| 4 | `void CloseAll()` | releases dictionaries |
| 5 | `long LastError` (get) | 0 = no error, 12 = licence expired, 13 = licence not valid for this computer, 20 = dictionary not loaded |

Flags: `0x1` include synonyms and lowest level terms, `0x2` include non-current terms, `0x4` current terms only.

The engine is not thread-safe: one instance per process, all calls from the thread that created it.
