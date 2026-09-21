SELECT "Id", "Kod", "Ad", "KategoriTanimiId", "MarkaTanimiId", "BirimTanimiId",
       "SirketId", "DepoId", "AlisFiyati", "PerakendeSatisFiyati", "ToptanSatisFiyati",
       "KdvOrani", "StokMiktari", "MinimumStok", "Aktif", "Silindi"
FROM "Urunler"
WHERE "Kod" = 'RT_P1_20260827_01';

SELECT column_name, is_nullable, data_type, column_default
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'Urunler'
ORDER BY ordinal_position;

SELECT con.conname, pg_get_constraintdef(con.oid)
FROM pg_constraint con
JOIN pg_class rel ON rel.oid = con.conrelid
WHERE rel.relname = 'Urunler';

SELECT "KullaniciAdi", "SifreHash" FROM "Kullanicilar" WHERE "KullaniciAdi" = 'admin';

SELECT "Id", "Unvan" FROM "Sirketler" WHERE "Id" = '11111111-1111-1111-1111-111111111111';
SELECT "Id", "Ad", "VarsayilanMi", "SubeId" FROM "Depolar" ORDER BY "Id";

SELECT "KullaniciId", "StokEkle", "Silindi" FROM "KullaniciYetkileri" WHERE "KullaniciId" = 1;

SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Urunler';
SELECT "Id", "Kod", "Barkod" FROM "Urunler" WHERE "Barkod" = '';
SELECT "Id", "Kod", "Ad", "OlusturmaTarihi", "Silindi" FROM "Urunler" ORDER BY "Id" DESC LIMIT 12;
SELECT "Id", "Kod", "Ad", "KategoriTanimiId", "MarkaTanimiId", "BirimTanimiId",
       "SirketId", "DepoId", "AlisFiyati", "PerakendeSatisFiyati", "ToptanSatisFiyati",
       "KdvOrani", "StokMiktari", "MinimumStok", "Aktif", "Silindi"
FROM "Urunler" WHERE "Kod" = 'RT_P1_FINAL_20260827';
