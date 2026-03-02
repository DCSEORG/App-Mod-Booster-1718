/*
  stored-procedures.sql
  All stored procedures for the Expense Management System
  Use: CREATE OR ALTER PROCEDURE to safely re-run
*/

SET NOCOUNT ON;
GO

-- =============================================
-- Get all expenses with optional filters
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllExpenses
    @StatusFilter NVARCHAR(50) = NULL,
    @Search       NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rb.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT  JOIN dbo.Users rb ON e.ReviewedBy = rb.UserId
    WHERE
        (@StatusFilter IS NULL OR s.StatusName = @StatusFilter)
        AND (
            @Search IS NULL
            OR e.Description LIKE '%' + @Search + '%'
            OR c.CategoryName LIKE '%' + @Search + '%'
            OR u.UserName LIKE '%' + @Search + '%'
        )
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Get a single expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rb.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT  JOIN dbo.Users rb ON e.ReviewedBy = rb.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Create a new expense (status = Draft)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId      INT,
    @CategoryId  INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DraftStatusId INT;
    SELECT @DraftStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @DraftStatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS NewExpenseId;
END
GO

-- =============================================
-- Submit an expense for approval (Draft -> Submitted)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';

    UPDATE dbo.Expenses
    SET
        StatusId    = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE
        ExpenseId = @ExpenseId
        AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
END
GO

-- =============================================
-- Approve a submitted expense (Submitted -> Approved)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_ApproveExpense
    @ExpenseId  INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';

    UPDATE dbo.Expenses
    SET
        StatusId   = @ApprovedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE
        ExpenseId = @ExpenseId
        AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
END
GO

-- =============================================
-- Reject a submitted expense (Submitted -> Rejected)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_RejectExpense
    @ExpenseId  INT,
    @ReviewedBy INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';

    UPDATE dbo.Expenses
    SET
        StatusId   = @RejectedStatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = SYSUTCDATETIME()
    WHERE
        ExpenseId = @ExpenseId
        AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
END
GO

-- =============================================
-- Delete an expense (only Draft expenses)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Expenses WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Get all submitted expenses (for manager approval view)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetSubmittedExpenses
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rb.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT  JOIN dbo.Users rb ON e.ReviewedBy = rb.UserId
    WHERE s.StatusName = 'Submitted'
    ORDER BY e.SubmittedAt ASC;
END
GO

-- =============================================
-- Get all active users
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllUsers
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- =============================================
-- Get all active expense categories
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllCategories
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CategoryId,
        CategoryName,
        IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO
