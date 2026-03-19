import psycopg2, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()

# Mevcut ders kodlari
cur.execute('SELECT "Code", "Id" FROM "Courses"')
existing = {r[0]: r[1] for r in cur.fetchall()}

# 1. Program (DepartmentId=5, EEM)
cur.execute('''INSERT INTO "AcademicPrograms" ("Name", "Code", "DepartmentId", "DegreeLevel", "EducationType", "MaxStudyDurationSemesters", "TotalRequiredEcts")
VALUES (%s, %s, %s, %s, %s, %s, %s) RETURNING "Id"''',
    ('Elektrik Elektronik M\u00fchendisli\u011fi Lisans', 'EEM-L', 5, 1, 0, 14, 240))
prog_id = cur.fetchone()[0]
print(f'Program: Id={prog_id}')

# 2. Mufredat
cur.execute('INSERT INTO "Curricula" ("ProgramId", "Version", "IsActive") VALUES (%s, %s, %s) RETURNING "Id"',
    (prog_id, 2024, True))
curr_id = cur.fetchone()[0]
print(f'Mufredat: Id={curr_id}')

# 3. Yeni dersler
new_courses = [
    # 2. yariyil
    ('EEE102', 'Introduction to EEE', 5, 3, 2, 0, False),
    # 3. yariyil
    ('MATH201', 'Vector and Complex Calculus', None, 6, 4, 0, False),
    ('EEE201', 'Circuit Theory I', 5, 6, 4, 0, False),
    # MATH204 zaten var
    # EEE203 zaten var
    ('EEE205', 'Computer Tools for Electrical Engineering', 5, 3, 2, 1, False),
    # 4. yariyil
    # MATH206 zaten var
    ('EEE202', 'Circuit Theory II', 5, 6, 4, 0, False),
    ('EEE208', 'Signals and Systems', 5, 5, 3, 0, False),
    ('EEE236', 'Electromagnetic Field Theory', 5, 5, 3, 0, False),
    ('EEE204', 'Electronics I', 5, 5, 3, 0, False),
    ('EEE212', 'Circuit Theory Lab', 5, 2, 0, 2, False),
    # 5. yariyil
    ('EEE301', 'Telecommunications I', 5, 6, 3, 2, False),
    ('EEE303', 'Electromagnetics Wave Theory', 5, 5, 3, 0, False),
    ('EEE313', 'Electronics II', 5, 5, 3, 0, False),
    ('EEE331', 'Electrical Machines', 5, 5, 3, 2, False),
    ('EEE321', 'Digital Signal Processing', 5, 5, 3, 0, False),
    ('EEE315', 'Electronics Lab', 5, 2, 0, 2, False),
    ('AEE301', 'Academic English V', None, 2, 2, 0, False),
    # 6. yariyil
    ('EEE302', 'Telecommunications II', 5, 5, 3, 2, False),
    ('EEE304', 'Control Theory', 5, 5, 3, 2, False),
    ('EEE306', 'Microprocessors', 5, 5, 3, 2, False),
    # 7. yariyil
    ('EEE491', 'Graduation Project I', 5, 5, 2, 2, False),
    # 8. yariyil
    ('EEE492', 'Graduation Project II', 5, 5, 3, 2, False),
    ('EEE490', 'Long-Term Practice', 5, 25, 0, 25, False),
    # Teknik Secmeli 3XX
    ('EEE312', 'Utilization of Electrical Energy', 5, 5, 3, 0, False),
    ('EEE322', 'Introduction to Power Systems', 5, 5, 3, 0, False),
    ('EEE324', 'Microwave Electronics', 5, 5, 3, 0, False),
    ('EEE326', 'CMOS VLSI Design, HDL', 5, 5, 3, 0, False),
    ('EEE328', 'Introduction to Robotics', 5, 5, 3, 0, False),
    ('EEE330', 'Electrical & Electronics Measurements', 5, 5, 3, 0, False),
    ('EEE332', 'Electrical Energy Transmission and Distribution', 5, 5, 3, 0, False),
    ('EEE334', 'Introduction to Machine Learning', 5, 5, 3, 0, False),
    ('EEE338', 'Optical Fiber Communications', 5, 5, 3, 0, False),
    # Teknik Secmeli 4XX
    ('EEE423', 'Antennas and Propagation', 5, 5, 3, 3, False),
    ('EEE455', 'Wireless Networking Technologies and Applications', 5, 5, 3, 3, False),
    ('EEE421', 'Digital Image Processing', 5, 5, 3, 3, False),
    ('EEE425', 'Process Control', 5, 5, 3, 3, False),
    ('EEE468', 'Numerical Methods in EE', 5, 5, 3, 3, False),
    ('EEE471', 'High Voltage Techniques', 5, 5, 3, 3, False),
    ('EEE431', 'Power System Analysis', 5, 5, 3, 3, False),
    ('EEE433', 'Artificial Intelligence', 5, 5, 3, 3, False),
    ('EEE477', 'Mobile Communications', 5, 5, 3, 3, False),
    ('EEE435', 'Power System Protection', 5, 5, 3, 3, False),
    ('EEE467', 'Renewable Energy', 5, 5, 3, 3, False),
    ('EEE484', 'Power Electronics', 5, 5, 3, 3, False),
    ('EEE472', 'Power System Design', 5, 5, 3, 3, False),
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
    ('CENG101',1,True), ('ENG101',1,True), ('MATH101',1,True), ('PHY101',1,True),
    ('PHY103',1,True), ('BIO101',1,True), ('HIS101',1,True), ('TUR101',1,True), ('OHS101',1,True),
    # 2. yariyil
    ('EEE102',2,True), ('OHS102',2,True), ('ENG102',2,True), ('MATH102',2,True),
    ('PHY102',2,True), ('PHY104',2,True), ('HIS102',2,True), ('TUR102',2,True), ('CENG102',2,True),
    # 3. yariyil
    ('MATH201',3,True), ('EEE201',3,True), ('MATH204',3,True), ('EEE203',3,True),
    ('EEE205',3,True), ('ENG201',3,True),
    # 4. yariyil
    ('MATH206',4,True), ('EEE202',4,True), ('EEE208',4,True), ('EEE236',4,True),
    ('EEE204',4,True), ('EEE212',4,True), ('ENG202',4,True),
    # 5. yariyil
    ('EEE301',5,True), ('EEE303',5,True), ('EEE313',5,True), ('EEE331',5,True),
    ('EEE321',5,True), ('EEE315',5,True), ('AEE301',5,True),
    # 6. yariyil
    ('EEE302',6,True), ('EEE304',6,True), ('EEE306',6,True), ('ENG302',6,True),
    # 7. yariyil
    ('EEE491',7,True), ('CCE401',7,True),
    # 8. yariyil
    ('EEE492',8,True), ('EEE490',8,True),
    # Teknik Secmeli 3XX (IsCompulsory=False, SuggestedSemester=0)
    ('EEE312',0,False), ('EEE322',0,False), ('EEE324',0,False), ('EEE326',0,False),
    ('EEE328',0,False), ('EEE330',0,False), ('EEE332',0,False), ('EEE334',0,False),
    ('EEE338',0,False),
    # Teknik Secmeli 4XX
    ('EEE423',0,False), ('EEE455',0,False), ('EEE421',0,False), ('EEE425',0,False),
    ('EEE468',0,False), ('EEE471',0,False), ('EEE431',0,False), ('EEE433',0,False),
    ('EEE477',0,False), ('EEE435',0,False), ('EEE467',0,False), ('EEE484',0,False),
    ('EEE472',0,False),
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
