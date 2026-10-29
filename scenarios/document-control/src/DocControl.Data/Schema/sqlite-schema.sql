CREATE TABLE Projects (
    Code        TEXT NOT NULL PRIMARY KEY,
    Name        TEXT NOT NULL,
    Client      TEXT NOT NULL,
    Active      INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE Documents (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Number        TEXT NOT NULL UNIQUE,
    ProjectCode   TEXT NOT NULL REFERENCES Projects(Code),
    Discipline    TEXT NOT NULL,
    DocumentType  TEXT NOT NULL,
    Title         TEXT NOT NULL,
    CreatedUtc    TEXT NOT NULL,
    CreatedBy     TEXT NOT NULL
);

CREATE TABLE Revisions (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    DocumentId     INTEGER NOT NULL REFERENCES Documents(Id),
    Code           TEXT NOT NULL,
    Status         INTEGER NOT NULL,
    FileName       TEXT NOT NULL,
    VaultPath      TEXT NOT NULL,
    Size           INTEGER NOT NULL,
    Sha256         TEXT NOT NULL,
    Source         TEXT NOT NULL,
    SubmittedBy    TEXT NOT NULL,
    SubmittedUtc   TEXT NOT NULL,
    ReviewedBy     TEXT NULL,
    ReviewedUtc    TEXT NULL,
    ReviewComment  TEXT NULL
);
CREATE INDEX IX_Revisions_Document ON Revisions(DocumentId);
CREATE INDEX IX_Revisions_Status ON Revisions(Status);

CREATE TABLE Transmittals (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    Number           TEXT NOT NULL UNIQUE,
    ProjectCode      TEXT NOT NULL REFERENCES Projects(Code),
    Purpose          INTEGER NOT NULL,
    RecipientCompany TEXT NOT NULL,
    RecipientName    TEXT NOT NULL,
    RecipientEmail   TEXT NOT NULL,
    Notes            TEXT NULL,
    Status           INTEGER NOT NULL,
    CreatedBy        TEXT NOT NULL,
    CreatedByEmail   TEXT NOT NULL,
    CreatedUtc       TEXT NOT NULL,
    IssuedUtc        TEXT NULL,
    PackagePath      TEXT NULL,
    PackageSize      INTEGER NULL,
    Error            TEXT NULL,
    Attempts         INTEGER NOT NULL DEFAULT 0,
    ClaimedBy        TEXT NULL,
    ClaimedUtc       TEXT NULL
);

CREATE TABLE TransmittalItems (
    TransmittalId  INTEGER NOT NULL REFERENCES Transmittals(Id),
    RevisionId     INTEGER NOT NULL REFERENCES Revisions(Id),
    PRIMARY KEY (TransmittalId, RevisionId)
);

CREATE TABLE DropFiles (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    FileId        TEXT NOT NULL,
    Sha256        TEXT NOT NULL,
    OriginalPath  TEXT NOT NULL,
    Result        TEXT NOT NULL,
    Reason        TEXT NULL,
    RevisionId    INTEGER NULL REFERENCES Revisions(Id),
    ProcessedUtc  TEXT NOT NULL
);
CREATE INDEX IX_DropFiles_Sha256 ON DropFiles(Sha256);
CREATE INDEX IX_DropFiles_FileId ON DropFiles(FileId);

CREATE TABLE Counters (
    Name   TEXT NOT NULL PRIMARY KEY,
    Value  INTEGER NOT NULL
);
