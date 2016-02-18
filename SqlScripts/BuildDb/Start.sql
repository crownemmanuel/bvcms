SET NUMERIC_ROUNDABORT OFF
GO
SET ANSI_PADDING, ANSI_WARNINGS, CONCAT_NULL_YIELDS_NULL, ARITHABORT, QUOTED_IDENTIFIER, ANSI_NULLS ON
GO
IF EXISTS (SELECT * FROM tempdb..sysobjects WHERE id=OBJECT_ID('tempdb..#tmpErrors')) DROP TABLE #tmpErrors
GO
CREATE TABLE #tmpErrors (Error int)
GO
SET XACT_ABORT ON
GO
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
GO
IF NOT EXISTS (SELECT * FROM master.dbo.syslogins WHERE loginname = N'ro-CMS_StarterDb')
CREATE LOGIN [ro-CMS_StarterDb] WITH PASSWORD = 'p@ssw0rd'
GO
CREATE USER [ro-CMS_StarterDb] FOR LOGIN [ro-CMS_StarterDb] WITH DEFAULT_SCHEMA=[dbo.]
GO
IF NOT EXISTS (SELECT * FROM master.dbo.syslogins WHERE loginname = N'ro-CMS_StarterDb-finance')
CREATE LOGIN [ro-CMS_StarterDb-finance] WITH PASSWORD = 'p@ssw0rd'
GO
CREATE USER [ro-CMS_StarterDb-finance] FOR LOGIN [ro-CMS_StarterDb-finance] WITH DEFAULT_SCHEMA=[dbo.]
GO
