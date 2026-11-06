param(
    [string]$OutputFile = "supabase-migration.sql",
    [string]$ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=CseReviewer;Trusted_Connection=True;TrustServerCertificate=True"
)

$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine(@"
-- ====================================================================
-- SUPABASE POSTGRESQL SCHEMA & DATA MIGRATION
-- Generated for Husay CSE Reviewer
--
-- Instructions:
-- 1. Open Supabase Dashboard: https://supabase.com/dashboard
-- 2. Click on the SQL Editor (the '>_' icon in the left menu)
-- 3. Click '+ New query', paste this entire file, and click 'Run' (Ctrl+Enter)
-- 4. Check 'Table Editor' (the grid icon) to see your tables and questions!
-- ====================================================================

-- Enable UUID extension if not enabled
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Drop existing tables if re-running
-- DROP TABLE IF EXISTS public."Sets" CASCADE;
-- DROP TABLE IF EXISTS public."BankQuestions" CASCADE;

-- 1. Create Sets Table
CREATE TABLE IF NOT EXISTS public."Sets" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Owner" VARCHAR(64) NOT NULL DEFAULT '',
    "Title" TEXT NOT NULL DEFAULT '',
    "Category" TEXT NOT NULL DEFAULT '',
    "Level" TEXT NOT NULL DEFAULT '',
    "Difficulty" TEXT NOT NULL DEFAULT '',
    "Source" TEXT NOT NULL DEFAULT '',
    "Count" INT NOT NULL DEFAULT 0,
    "Saved" BOOLEAN NOT NULL DEFAULT FALSE,
    "Created" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "Questions" TEXT NOT NULL DEFAULT '[]',
    "Progress" TEXT NOT NULL DEFAULT '[]',
    "Focus" TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS "IX_Sets_Owner_Saved" ON public."Sets" ("Owner", "Saved");

-- 2. Create BankQuestions Table
CREATE TABLE IF NOT EXISTS public."BankQuestions" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Owner" VARCHAR(64) NOT NULL DEFAULT '',
    "Fingerprint" VARCHAR(64) NOT NULL,
    "Category" VARCHAR(64) NOT NULL,
    "Level" VARCHAR(32) NOT NULL,
    "Difficulty" VARCHAR(16) NOT NULL,
    "Source" VARCHAR(200) NOT NULL,
    "Data" TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_BankQuestions_Owner_Fingerprint" 
ON public."BankQuestions" ("Owner", "Fingerprint");

-- Disable RLS initially so backend can read/write freely, or set up policies later
ALTER TABLE public."Sets" DISABLE ROW LEVEL SECURITY;
ALTER TABLE public."BankQuestions" DISABLE ROW LEVEL SECURITY;

"@)

$conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()

# 1. Export unique BankQuestions
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
WITH Ranked AS (
    SELECT Id, Owner, Fingerprint, Category, Level, Difficulty, Source, Data,
           ROW_NUMBER() OVER(PARTITION BY Fingerprint ORDER BY Id) as rn
    FROM dbo.BankQuestions
)
SELECT Id, Owner, Fingerprint, Category, Level, Difficulty, Source, Data
FROM Ranked
WHERE rn = 1;
"@
$reader = $cmd.ExecuteReader()
$qInserts = New-Object System.Collections.Generic.List[string]

while ($reader.Read()) {
    $id = $reader["Id"].ToString()
    $owner = $reader["Owner"].ToString().Replace("'", "''")
    $fp = $reader["Fingerprint"].ToString().Replace("'", "''")
    $cat = $reader["Category"].ToString().Replace("'", "''")
    $lvl = $reader["Level"].ToString().Replace("'", "''")
    $diff = $reader["Difficulty"].ToString().Replace("'", "''")
    $src = $reader["Source"].ToString().Replace("'", "''")
    $data = $reader["Data"].ToString().Replace("'", "''")
    $qInserts.Add("('$id', '$owner', '$fp', '$cat', '$lvl', '$diff', '$src', '$data')")
}
$reader.Close()

if ($qInserts.Count -gt 0) {
    [void]$sb.AppendLine("-- 3. Seed BankQuestions ($($qInserts.Count) unique questions)")
    [void]$sb.AppendLine('INSERT INTO public."BankQuestions" ("Id", "Owner", "Fingerprint", "Category", "Level", "Difficulty", "Source", "Data") VALUES')
    [void]$sb.AppendLine(($qInserts -join ",`n"))
    [void]$sb.AppendLine('ON CONFLICT ("Owner", "Fingerprint") DO NOTHING;')
    [void]$sb.AppendLine()
}

$conn.Close()

Set-Content -Path $OutputFile -Value $sb.ToString() -Encoding UTF8
Write-Output "Successfully generated $OutputFile with $($qInserts.Count) questions."
