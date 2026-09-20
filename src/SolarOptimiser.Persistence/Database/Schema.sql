-- SOL-T-401: Solar Optimiser Phase 1 MariaDB schema.

CREATE TABLE Sites (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  ProviderKey VARCHAR(32) NOT NULL,
  ProviderSiteID VARCHAR(64) NOT NULL,
  Name VARCHAR(128) NOT NULL,
  TimeZone VARCHAR(64) NULL,
  CreatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  UpdatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  UNIQUE KEY UQ_Sites_Provider (ProviderKey, ProviderSiteID)
);

CREATE TABLE Devices (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  SiteID BIGINT UNSIGNED NOT NULL,
  ProviderDeviceID VARCHAR(64) NOT NULL,
  ModuleSerial VARCHAR(64) NULL,
  Status VARCHAR(32) NOT NULL,
  Model VARCHAR(64) NULL,
  HasPV TINYINT(1) NOT NULL,
  HasBattery TINYINT(1) NOT NULL,
  LastAlertedOutcome VARCHAR(24) NULL,
  CreatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  UpdatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  UNIQUE KEY UQ_Devices_SiteProvider (SiteID, ProviderDeviceID),
  CONSTRAINT FK_Devices_Site FOREIGN KEY (SiteID) REFERENCES Sites(ID)
);

CREATE TABLE DeviceCapabilities (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  DeviceID BIGINT UNSIGNED NOT NULL,
  SourceVariable VARCHAR(64) NOT NULL,
  Unit VARCHAR(16) NULL,
  IsExpected TINYINT(1) NOT NULL DEFAULT 0,
  ExpectedSince DATETIME(3) NULL,
  RetiredAtUTC DATETIME(3) NULL,
  DiscoveredAtUTC DATETIME(3) NOT NULL,
  LastSeenAtUTC DATETIME(3) NOT NULL,
  UNIQUE KEY UQ_DeviceCapabilities_Device (DeviceID, SourceVariable),
  CONSTRAINT FK_DeviceCapabilities_Device FOREIGN KEY (DeviceID) REFERENCES Devices(ID)
);

CREATE TABLE DeviceBatteries (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  DeviceID BIGINT UNSIGNED NOT NULL,
  BatterySerial VARCHAR(64) NOT NULL,
  BatteryType VARCHAR(32) NULL,
  Model VARCHAR(64) NULL,
  CapacityRaw VARCHAR(32) NULL,
  ManufacturedAtRaw VARCHAR(32) NULL,
  DiscoveredAtUTC DATETIME(3) NOT NULL,
  UNIQUE KEY UQ_DeviceBatteries_Device (DeviceID, BatterySerial),
  CONSTRAINT FK_DeviceBatteries_Device FOREIGN KEY (DeviceID) REFERENCES Devices(ID)
);

CREATE TABLE CollectionRuns (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  StartedAtUTC DATETIME(3) NOT NULL,
  CompletedAtUTC DATETIME(3) NULL,
  DevicesAttempted INT NOT NULL DEFAULT 0,
  DevicesSucceeded INT NOT NULL DEFAULT 0,
  ObservationsWritten INT NOT NULL DEFAULT 0,
  Status VARCHAR(16) NOT NULL, -- Success | PartialFailure | Failed
  StatusCheckedAtUTC DATETIME(3) NULL,
  StatusHTTPStatus SMALLINT NULL,
  StatusProviderErrorNumber INT NULL,
  StatusProviderMessage VARCHAR(256) NULL,
  StatusRequestPath VARCHAR(260) NULL,
  StatusResponsePath VARCHAR(260) NULL
);

CREATE TABLE CollectionAttempts (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  CollectionRunID BIGINT UNSIGNED NOT NULL,
  DeviceID BIGINT UNSIGNED NOT NULL,
  RequestedAtUTC DATETIME(3) NOT NULL,
  CompletedAtUTC DATETIME(3) NULL,
  Outcome VARCHAR(24) NOT NULL, -- Success | PartialMissing | DeviceFaultOrOffline | Throttled | AuthFailed | ValidationFailed | ProviderServerError | Transport | ParseFailure
  DeviceStatus VARCHAR(32) NULL, -- denormalized from the run's shared status call; drives the trust gate (SOL-T-601)
  HTTPStatus SMALLINT NULL,
  ProviderErrorNumber INT NULL,
  ProviderMessage VARCHAR(256) NULL,
  RequestedVariables TEXT NULL, -- null = "all" (every poll omits FoxESS's variables parameter)
  ReturnedVariables TEXT NULL,
  RequestPath VARCHAR(260) NULL,
  RawResponsePath VARCHAR(260) NULL,
  SentryEventID CHAR(32) NULL, -- SentryId.ToString("n"); null if not alerted or Sentry unavailable
  CONSTRAINT FK_CollectionAttempts_Run FOREIGN KEY (CollectionRunID) REFERENCES CollectionRuns(ID),
  CONSTRAINT FK_CollectionAttempts_Device FOREIGN KEY (DeviceID) REFERENCES Devices(ID),
  KEY IX_CollectionAttempts_DeviceTime (DeviceID, RequestedAtUTC DESC)
);

CREATE TABLE TelemetryObservations (
  ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  CaptureID BIGINT UNSIGNED NOT NULL, -- device is derived via CaptureID -> CollectionAttempts.DeviceID; never stored here directly
  Quantity VARCHAR(32) NOT NULL,
  Channel VARCHAR(16) NULL, -- raw vendor channel only, e.g. "3" for pv3Power; no physical mapping implied
  ValueParsed DECIMAL(14,4) NULL, -- parsed/precision-limited; the exact provider lexical value lives in the raw evidence file
  Unit VARCHAR(16) NULL, -- exactly as FoxESS reported it; no conversion
  Quality VARCHAR(16) NOT NULL, -- Ok | Missing | Invalid | Stale
  SourceVariable VARCHAR(64) NOT NULL,
  MappingVersion SMALLINT NOT NULL, -- the mapping-set version active when this row was written
  ProviderTimestampRaw VARCHAR(64) NULL,
  ObservedAtUTC DATETIME(3) NULL,
  ObservedAtParseStatus VARCHAR(16) NOT NULL, -- Parsed | Unparseable | NotAttempted
  RetrievedAtUTC DATETIME(3) NOT NULL,
  CreatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  KEY IX_TelemetryObservations_Capture (CaptureID, Quantity, Channel),
  CONSTRAINT FK_TelemetryObservations_Capture FOREIGN KEY (CaptureID) REFERENCES CollectionAttempts(ID)
);
-- "latest per device/quantity" is served by joining CollectionAttempts(DeviceID, RequestedAtUTC DESC)
-- into this table's capture index; TelemetryObservations never denormalizes DeviceID (see SOL-T-402).
