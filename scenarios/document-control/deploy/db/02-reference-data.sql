-- Active projects. Maintained by document control; add new projects here or directly in the table.
MERGE dc.Projects AS t
USING (VALUES
    ('P1042', N'Kemira Oulu bleach plant upgrade', N'Pohjola Pulp Oy', 1),
    ('P1057', N'Harbour terminal conveyor line 3', N'Northwind Ports', 1),
    ('P0988', N'Riverside water treatment', N'City of Riverside', 0)
) AS s (Code, Name, Client, Active)
ON t.Code = s.Code
WHEN MATCHED THEN UPDATE SET Name = s.Name, Client = s.Client, Active = s.Active
WHEN NOT MATCHED THEN INSERT (Code, Name, Client, Active) VALUES (s.Code, s.Name, s.Client, s.Active);
GO
