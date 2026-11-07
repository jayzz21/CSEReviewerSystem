-- Run in Supabase SQL Editor before deploying the multiplayer update.
BEGIN;
SET LOCAL search_path TO public;
CREATE TABLE IF NOT EXISTS "Lobbies" ("Code" varchar(10) PRIMARY KEY, "Data" text NOT NULL);
ALTER TABLE "Lobbies" ENABLE ROW LEVEL SECURITY;
COMMIT;
