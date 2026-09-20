DROP PROCEDURE IF EXISTS espSiteGetByProviderID;

DELIMITER $$

CREATE PROCEDURE espSiteGetByProviderID(
  IN in_providerKey VARCHAR(32),
  IN in_providerSiteId VARCHAR(64)
)
BEGIN
  SELECT
    ID AS 's.ID',
    ProviderKey AS 's.ProviderKey',
    ProviderSiteID AS 's.ProviderSiteID',
    Name AS 's.Name',
    TimeZone AS 's.TimeZone',
    CreatedAtUTC AS 's.CreatedAtUTC',
    UpdatedAtUTC AS 's.UpdatedAtUTC'
  FROM Sites
  WHERE ProviderKey = in_providerKey AND ProviderSiteID = in_providerSiteId;
END$$

DELIMITER ;
