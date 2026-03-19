import psycopg2, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()

# Mevcut ders kodlari
cur.execute('SELECT "Code", "Id" FROM "Courses"')
existing = {r[0]: r[1] for r in cur.fetchall()}

# 1. Program (DepartmentId=6, YM)
cur.execute('''INSERT INTO "AcademicPrograms" ("Name", "Code", "DepartmentId", "DegreeLevel", "EducationType", "MaxStudyDurationSemesters", "TotalRequiredEcts")
VALUES (%s, %s, %s, %s, %s, %s, %s) RETURNING "Id"''',
    ('Yaz\u0131l\u0131m M\u00fchendisli\u011fi Lisans', 'YM-L', 6, 1, 0, 14, 240))
prog_id = cur.fetchone()[0]
print(f'Program: Id={prog_id}')

# 2. Mufredat
cur.execute('INSERT INTO "Curricula" ("ProgramId", "Version", "IsActive") VALUES (%s, %s, %s) RETURNING "Id"',
    (prog_id, 2024, True))
curr_id = cur.fetchone()[0]
print(f'Mufredat: Id={curr_id}')

# 3. Yeni dersler (sadece mevcut olmayanlar)
new_courses = [
    # 6. yariyil
    ('SENG322', 'Software Validation and Verification', 6, 6, 3, 0, False),
    ('SENG324', 'Software Design Patterns', 6, 5, 3, 0, False),
    ('CENG384', 'Intro. to Signals Processing for Comp. Engineers', 6, 5, 3, 0, False),
    # 7. yariyil
    ('SENG491', 'Graduation Project I', 6, 5, 3, 2, False),
    # 8. yariyil
    ('SENG492', 'Graduation Project II', 6, 5, 3, 2, False),
    ('SENG490', 'Long Term Practice', 6, 25, 0, 16, False),
]

course_ids = dict(existing)
added = 0
for code, name, dept, ects, theory, lab, nongpa in new_courses:
    if code in course_ids:
        continue
    cur.execute('''INSERT INTO "Courses" ("Code", "Name", "DepartmentId", "Ects", "TheoryHours", "LabHours", "IsNonGpa")
        VALUES (%s,%s,%s,%s,%s,%s,%s) RETURNING "Id"''',
        (code, name, dept, ects, theory, lab, nongpa))
    course_ids[code] = cur.fetchone()[0]
    added += 1

print(f'{added} yeni ders eklendi')

# 4. CurriculumCourses
cc = [
    # 1. yariyil
    ('CENG101',1,True), ('OHS101',1,True), ('ENG101',1,True), ('MATH101',1,True),
    ('PHY101',1,True), ('PHY103',1,True), ('BIO101',1,True), ('HIS101',1,True), ('TUR101',1,True),
    # 2. yariyil
    ('CENG102',2,True), ('OHS102',2,True), ('ENG102',2,True), ('MATH102',2,True),
    ('PHY102',2,True), ('PHY104',2,True), ('HIS102',2,True), ('TUR102',2,True),
    # 3. yariyil
    ('SENG231',3,True), ('EEE203',3,True), ('CENG213',3,True), ('CENG223',3,True),
    ('CENG241',3,True), ('ENG201',3,True),
    # 4. yariyil
    ('CENG214',4,True), ('CENG256',4,True), ('MATH224',4,True), ('MATH206',4,True),
    ('SENG264',4,True), ('ENG202',4,True),
    # 5. yariyil
    ('CENG301',5,True), ('CENG351',5,True), ('SENG321',5,True),
    ('CENG361',5,True), ('ENG301',5,True),
    # 6. yariyil
    ('SENG322',6,True), ('SENG324',6,True), ('CENG336',6,True),
    ('CENG384',6,True), ('ENG302',6,True),
    # 7. yariyil
    ('SENG491',7,True), ('CCE401',7,True),
    # 8. yariyil
    ('SENG492',8,True), ('SENG490',8,True),
]

for code, sem, comp in cc:
    cid = course_ids[code]
    cur.execute('INSERT INTO "CurriculumCourses" ("CurriculumId", "CourseId", "SuggestedSemester", "IsCompulsory") VALUES (%s,%s,%s,%s)',
        (curr_id, cid, sem, comp))

zorunlu = sum(1 for _,_,c in cc if c)
secmeli = sum(1 for _,_,c in cc if not c)
print(f'{zorunlu} zorunlu + {secmeli} secmeli = {len(cc)} mufredat dersi eklendi')

conn.commit()
conn.close()
print('Tamamlandi!')
