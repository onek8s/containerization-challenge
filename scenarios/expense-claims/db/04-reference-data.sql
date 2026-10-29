/*
  Company cards known at go-live (from the card company's card holder list).
  Later cards are linked in the portal under Company cards.
*/
SET NOCOUNT ON;

MERGE [exp].[CompanyCards] AS t
USING (VALUES
    (N'552235******4410', N'100233'),
    (N'552235******7021', N'100150')
) AS s (MaskedNumber, EmployeeNumber)
ON t.MaskedNumber = s.MaskedNumber
WHEN NOT MATCHED AND EXISTS (SELECT 1 FROM [exp].[Employees] e WHERE e.EmployeeNumber = s.EmployeeNumber) THEN
    INSERT (MaskedNumber, EmployeeId, Active)
    VALUES (s.MaskedNumber, (SELECT e.Id FROM [exp].[Employees] e WHERE e.EmployeeNumber = s.EmployeeNumber), 1);
GO
