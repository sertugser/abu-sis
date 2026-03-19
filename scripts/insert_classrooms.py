import psycopg2, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()

# Mevcut derslikleri sil (CourseSections'da referans varsa once onlari temizle)
cur.execute('UPDATE "CourseSections" SET "ClassroomId" = NULL WHERE "ClassroomId" IS NOT NULL')
cur.execute('DELETE FROM "Classrooms"')
print('Eski derslikler silindi')

# Yeni derslikler
# Type: 0=Lecture, 1=Lab, 2=Seminar, 3=Computer
classrooms = [
    ('AMF\u0130 1', 110, 0),
    ('Amfi - S\u0131n\u0131f-Yeni-Kat S\u0131f\u0131r', 80, 0),
    ('C 017', 70, 0),
    ('C 018', 70, 0),
    ('C 025', 70, 0),
    ('C 026', 70, 0),
    ('C 036', 80, 0),
    ('C 054', 30, 0),
    ('C 005', 50, 0),
    ('L139 Ceng Lab-1', 30, 1),
    ('L141 Ceng Lab-2', 30, 1),
    ('Ceng Lab-3 (Kat 0)', 30, 1),
    ('L140 EEE LAB', 30, 1),
    ('L143 PHY LAB', 30, 1),
]

for name, cap, ctype in classrooms:
    cur.execute('INSERT INTO "Classrooms" ("Name", "Capacity", "Type") VALUES (%s, %s, %s) RETURNING "Id"',
        (name, cap, ctype))
    cid = cur.fetchone()[0]
    print(f'  Id={cid} | {name} ({cap} kisi)')

conn.commit()
conn.close()
print('Tamamlandi!')
