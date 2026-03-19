import psycopg2, sys, io, uuid
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()

# Mevcut ogrenci kullanicisinin password hash'ini al (hepsi ayni sifre: Ogrenci123!)
cur.execute('SELECT "PasswordHash" FROM "AspNetUsers" WHERE "Email" = %s', ('ogrenci@ankarabilim.edu.tr',))
pwd_hash = cur.fetchone()[0]

# Student rolunun Id'sini al
cur.execute('SELECT "Id" FROM "AspNetRoles" WHERE "Name" = %s', ('Student',))
student_role_id = cur.fetchone()[0]

# Advisor Id (tek ogretmen var)
advisor_id = 1

# Ogrenci verileri: (FirstName, LastName, email_prefix, StudentNumber, ProgramId, CurriculumId, Cgpa, CompletedSemesters)
students = [
    # BM Ingilizce (Dept 1, Prog 1, Curr 1) - 2 mevcut, 1 eksik (1.70-2.0 arasi)
    ('Burak', 'Demir', 'ogrenci3', '2201002', 1, 1, 1.85, 4),

    # BM Turkce (Dept 2, Prog 2, Curr 2)
    ('Zeynep', 'Arslan', 'ogrenci4', '2301001', 2, 2, 2.35, 4),
    ('Mert', 'Sahin', 'ogrenci5', '2301002', 2, 2, 1.78, 4),
    ('Elif', 'Ozdemir', 'ogrenci6', '2301003', 2, 2, 1.45, 4),

    # Bilisim Sistemleri (Dept 3, Prog 3, Curr 3) - kullanicinin verdigi isimler
    ('Sergen', 'Yalcin', 'ogrenci7', '2203001', 3, 3, 2.72, 4),
    ('Arthur', 'Morgan', 'ogrenci8', '2203002', 3, 3, 1.88, 4),
    ('Orkun', 'Kokcu', 'ogrenci9', '2203003', 3, 3, 1.52, 4),

    # Endustri (Dept 4, Prog 4, Curr 4)
    ('Ceren', 'Yildiz', 'ogrenci10', '2204001', 4, 4, 2.91, 4),
    ('Kaan', 'Celik', 'ogrenci11', '2204002', 4, 4, 1.74, 4),
    ('Deniz', 'Kurt', 'ogrenci12', '2204003', 4, 4, 1.38, 4),

    # Elektrik Elektronik (Dept 5, Prog 5, Curr 5)
    ('Baran', 'Aksoy', 'ogrenci13', '2205001', 5, 5, 3.15, 4),
    ('Selin', 'Dogan', 'ogrenci14', '2205002', 5, 5, 1.92, 4),
    ('Ege', 'Koç', 'ogrenci15', '2205003', 5, 5, 1.58, 4),

    # Yazilim (Dept 6, Prog 6, Curr 6)
    ('Yusuf', 'Aydin', 'ogrenci16', '2206001', 6, 6, 2.56, 4),
    ('Irem', 'Tas', 'ogrenci17', '2206002', 6, 6, 1.80, 4),
    ('Can', 'Polat', 'ogrenci18', '2206003', 6, 6, 1.65, 4),
]

for first, last, email_pfx, snum, prog_id, curr_id, cgpa, semesters in students:
    email = f'{email_pfx}@ankarabilim.edu.tr'
    username = email
    security_stamp = str(uuid.uuid4())
    concurrency_stamp = str(uuid.uuid4())

    # 1. AspNetUsers
    cur.execute('''INSERT INTO "AspNetUsers"
        ("FirstName", "LastName", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
         "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
         "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s) RETURNING "Id"''',
        (first, last, username, username.upper(), email, email.upper(),
         True, pwd_hash, security_stamp, concurrency_stamp,
         False, False, True, 0))
    user_id = cur.fetchone()[0]

    # 2. AspNetUserRoles
    cur.execute('INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES (%s, %s)',
        (user_id, student_role_id))

    # 3. Students
    # Status: 0 = Active
    cur.execute('''INSERT INTO "Students"
        ("UserId", "StudentNumber", "NationalId", "Status", "Cgpa", "Sgpa",
         "CompletedSemesters", "FrozenSemesterCount", "WithdrawalCount",
         "IsScholarshipStudent", "HasFinancialBlock", "AdvisorId")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s) RETURNING "Id"''',
        (user_id, snum, f'1{snum}00000', 0, cgpa, cgpa,
         semesters, 0, 0, False, False, advisor_id))
    student_id = cur.fetchone()[0]

    # 4. StudentPrograms
    cur.execute('''INSERT INTO "StudentPrograms"
        ("StudentId", "ProgramId", "CurriculumId", "Type", "EnrollmentDate", "IsActive")
        VALUES (%s,%s,%s,%s,%s,%s)''',
        (student_id, prog_id, curr_id, 0, '2022-09-15', True))

    print(f'  Student Id={student_id} | UserId={user_id} | {first} {last} | {snum} | CGPA={cgpa} | Prog={prog_id}')

conn.commit()
conn.close()
print(f'\n{len(students)} ogrenci eklendi. Tamamlandi!')
print('Tum ogrenciler icin sifre: Ogrenci123!')
