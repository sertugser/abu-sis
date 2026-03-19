import psycopg2, sys, io, uuid
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()

# Password hash ve role id
cur.execute('SELECT "PasswordHash" FROM "AspNetUsers" WHERE "Email" = %s', ('ogrenci@ankarabilim.edu.tr',))
pwd_hash = cur.fetchone()[0]

cur.execute('SELECT "Id" FROM "AspNetRoles" WHERE "Name" = %s', ('Instructor',))
instructor_role_id = cur.fetchone()[0]

tr_map = str.maketrans('\u00e7\u011f\u0131\u00f6\u015f\u00fc\u00c7\u011e\u0130\u00d6\u015e\u00dc', 'cgiosuCGIOSU')

def add_instructor(first, last, title, dept_id):
    email = f'{first.lower().replace(" ", "").replace(".", "")}.{last.lower()}@ankarabilim.edu.tr'
    email = email.translate(tr_map)

    # Zaten var mi kontrol et
    cur.execute('SELECT "Id" FROM "AspNetUsers" WHERE "Email" = %s', (email,))
    if cur.fetchone():
        print(f'  ATLA (zaten var): {title} {first} {last}')
        return

    security_stamp = str(uuid.uuid4())
    concurrency_stamp = str(uuid.uuid4())

    cur.execute('''INSERT INTO "AspNetUsers"
        ("FirstName", "LastName", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
         "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
         "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s) RETURNING "Id"''',
        (first, last, email, email.upper(), email, email.upper(),
         True, pwd_hash, security_stamp, concurrency_stamp,
         False, False, True, 0))
    user_id = cur.fetchone()[0]

    cur.execute('INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES (%s, %s)',
        (user_id, instructor_role_id))

    cur.execute('''INSERT INTO "Instructors" ("UserId", "DepartmentId", "Title", "MaxWeeklyHours")
        VALUES (%s, %s, %s, %s) RETURNING "Id"''',
        (user_id, dept_id, title, 20))
    inst_id = cur.fetchone()[0]
    print(f'  Id={inst_id} | {title} {first} {last} | {email} | BolumId={dept_id}')

# === ENDUSTRI MUHENDISLIGI (DeptId=4) ===
print('\n=== Endustri Muhendisligi ===')
em_instructors = [
    ('Yavuz Selim', '\u00d6zdemir', 'Do\u00e7. Dr.', 4),
    ('Benh\u00fcr', 'Sat\u0131r', 'Do\u00e7. Dr.', 4),
    ('Orhan', 'Gerdan', 'Dr.', 4),
    ('Arda', 'Alp', 'Dr.', 4),
    ('Burcu Devrim', '\u0130\u00e7tenba\u015f', 'Dr.', 4),
    ('Mustafa', 'Akkaya', 'Dr.', 4),
    # Seher Fisekci Par zaten BM'de var, EM'de de ders verebilir - atla
    ('Baycan', '\u0130brahimo\u011flu', 'Emeritus Prof. Dr.', 4),
    ('\u00d6zg\u00fcr', 'Erol', 'Do\u00e7. Dr.', 4),
    ('Deniz', 'Okul', 'Dr.', 4),
    ('G\u00fcne\u015fcan', '\u00c7etik', 'Ar\u015f. G\u00f6r.', 4),
]

for first, last, title, dept in em_instructors:
    add_instructor(first, last, title, dept)

# === ELEKTRIK ELEKTRONIK MUHENDISLIGI (DeptId=5) ===
print('\n=== Elektrik Elektronik Muhendisligi ===')
eem_instructors = [
    ('Halim Haldun', 'G\u00f6kta\u015f', 'Prof. Dr.', 5),
    ('Alas', 'Eleyan', 'Do\u00e7. Dr.', 5),
    ('Hamed', 'Kaghazchi', 'Dr.', 5),
    ('Erc\u00fcment', 'Karap\u0131nar', 'Dr.', 5),
    ('Ay\u015fe', '\u00d6zdemir', 'Dr.', 5),
    ('Vedat', 'Tanr\u0131verdi', 'Dr.', 5),
    ('Yusuf', 'Uzun', 'Dr.', 5),
    ('Parisa', 'Akar', 'Dr.', 5),
    ('Taha', 'Selik', 'Ar\u015f. G\u00f6r.', 5),
]

for first, last, title, dept in eem_instructors:
    add_instructor(first, last, title, dept)

conn.commit()
conn.close()
print('\nTamamlandi!')
