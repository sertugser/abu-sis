import psycopg2, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()

# Mevcut ders kodlari
cur.execute('SELECT "Code", "Id" FROM "Courses"')
existing = {r[0]: r[1] for r in cur.fetchall()}

# 1. Program (DepartmentId=4, EM)
cur.execute('''INSERT INTO "AcademicPrograms" ("Name", "Code", "DepartmentId", "DegreeLevel", "EducationType", "MaxStudyDurationSemesters", "TotalRequiredEcts")
VALUES (%s, %s, %s, %s, %s, %s, %s) RETURNING "Id"''',
    ('End\u00fcstri M\u00fchendisli\u011fi Lisans', 'EM-L', 4, 1, 0, 14, 240))
prog_id = cur.fetchone()[0]
print(f'Program: Id={prog_id}')

# 2. Mufredat
cur.execute('INSERT INTO "Curricula" ("ProgramId", "Version", "IsActive") VALUES (%s, %s, %s) RETURNING "Id"',
    (prog_id, 2023, True))
curr_id = cur.fetchone()[0]
print(f'Mufredat: Id={curr_id}')

# 3. Yeni dersler
new_courses = [
    ('IE101', 'Introduction to Industrial Engineering', 4, 5, 3, 0, False),
    ('IE112', 'Computer Aided Technical Drawing', 4, 3, 1, 2, False),
    ('IE241', 'Engineering Economy', 4, 5, 3, 0, False),
    ('IE227', 'Probability Theory', 4, 5, 3, 0, False),
    ('IE223', 'Materials Science', 4, 5, 3, 0, False),
    ('MATH213', 'Linear Algebra', None, 5, 3, 0, False),
    ('MATH215', 'Differential Equations', None, 5, 3, 0, False),
    ('IE202', 'Work Study and Ergonomics', 4, 5, 3, 0, False),
    ('IE224', 'Manufacturing Processes', 4, 5, 3, 0, False),
    ('IE228', 'Engineering Statistics', 4, 5, 3, 0, False),
    ('IE232', 'Operations Research I', 4, 5, 3, 0, False),
    ('IE242', 'Cost Analysis in Engineering', 4, 5, 3, 0, False),
    ('IE301', 'Facilities Planning', 4, 5, 3, 0, False),
    ('IE333', 'Operations Research II', 4, 5, 3, 0, False),
    ('IE327', 'System Simulation', 4, 5, 3, 0, False),
    ('IE365', 'Production Planning and Control I', 4, 5, 3, 0, False),
    ('IE325', 'Quality Engineering', 4, 5, 3, 0, False),
    ('IE324', 'Project Management', 4, 3, 3, 0, False),
    ('IE366', 'Production Planning and Control II', 4, 5, 3, 0, False),
    ('IE346', 'Decision Analysis', 4, 5, 3, 0, False),
    ('IE491', 'Graduation Project I', 4, 5, 3, 2, False),
    ('IE489', 'Seminars in IE Practice', 4, 3, 2, 0, False),
    ('IE492', 'Graduation Project II', 4, 5, 3, 2, False),
    ('IE490', 'Work Place Training', 4, 25, 0, 16, False),
    # Teknik Secmeli 3XX
    ('IE352', 'Technology Management and Innovation', 4, 5, 3, 0, False),
    ('IE354', 'Time Series Analysis and Forecasting', 4, 5, 3, 0, False),
    ('IE356', 'Risk Management', 4, 5, 3, 0, False),
    ('IE358', 'Scheduling', 4, 5, 3, 0, False),
    ('IE362', 'Supply Chain Management', 4, 5, 3, 0, False),
    ('IE364', 'Logistics', 4, 5, 3, 0, False),
    ('IE372', 'Statistical Process Control', 4, 5, 3, 0, False),
    ('IE374', 'Game Theory', 4, 5, 3, 0, False),
    ('IE376', 'Management Information Systems', 4, 5, 3, 0, False),
    ('IE380', 'Fundamentals of Business and Management', 4, 5, 3, 0, False),
    ('IE382', 'Business Communication', 4, 5, 3, 0, False),
    ('IE384', 'Organizational Behavior', 4, 5, 3, 0, False),
    ('IE386', 'Customer Relationship Management', 4, 5, 3, 0, False),
    ('IE388', 'Introduction to Sustainability', 4, 5, 3, 0, False),
    ('EEE318', 'Matlab Applications in Electrical Engineering', 5, 5, 3, 0, False),
    ('EEE320', 'Neural Networks', 5, 5, 3, 0, False),
    # Teknik Secmeli 4XX
    ('IE451', 'Data Science', 4, 5, 3, 0, False),
    ('IE453', 'Applied Data Analysis', 4, 5, 3, 0, False),
    ('IE455', 'Statistics with R', 4, 5, 3, 0, False),
    ('IE457', 'Design and Analysis of Experiments', 4, 5, 3, 0, False),
    ('IE461', 'Strategic Management and Planning', 4, 5, 3, 0, False),
    ('IE463', 'Decision Support Systems', 4, 5, 3, 0, False),
    ('IE465', 'Human Resources Management', 4, 5, 3, 0, False),
    ('IE467', 'Business Intelligence and Analytics', 4, 5, 3, 0, False),
    ('IE469', 'Computer Programming in IE', 4, 5, 3, 0, False),
    ('IE471', 'Applied Operations Research', 4, 5, 3, 0, False),
    ('IE473', 'Stochastic Models', 4, 5, 3, 0, False),
    ('IE475', 'System Dynamics Modeling', 4, 5, 3, 0, False),
    ('IE477', 'Pricing and Revenue Optimization', 4, 5, 3, 0, False),
    ('IE479', 'Flexible Production Systems', 4, 5, 3, 0, False),
    ('IE481', 'Quality Management Systems', 4, 5, 3, 0, False),
    ('ISE361', 'Internet of Things', 3, 5, 3, 0, False),
    ('CENG375', 'Machine Learning', 1, 5, 3, 0, False),
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
    ('OHS101',1,True), ('ENG101',1,True), ('MATH101',1,True), ('PHY101',1,True),
    ('PHY103',1,True), ('IE101',1,True), ('CENG101',1,True), ('HIS101',1,True), ('TUR101',1,True),
    # 2. yariyil
    ('OHS102',2,True), ('ENG102',2,True), ('MATH102',2,True), ('PHY102',2,True),
    ('PHY104',2,True), ('IE112',2,True), ('CENG102',2,True), ('HIS102',2,True), ('TUR102',2,True),
    # 3. yariyil
    ('IE241',3,True), ('IE227',3,True), ('IE223',3,True), ('MATH213',3,True),
    ('MATH215',3,True), ('ENG201',3,True),
    # 4. yariyil
    ('IE202',4,True), ('IE224',4,True), ('IE228',4,True), ('IE232',4,True),
    ('IE242',4,True), ('ENG202',4,True),
    # 5. yariyil
    ('IE301',5,True), ('IE333',5,True), ('IE327',5,True), ('IE365',5,True),
    ('IE325',5,True), ('ENG301',5,True),
    # 6. yariyil
    ('IE324',6,True), ('IE366',6,True), ('IE346',6,True), ('ENG302',6,True),
    # 7. yariyil
    ('IE491',7,True), ('IE489',7,True), ('CCE401',7,True),
    # 8. yariyil
    ('IE492',8,True), ('IE490',8,True),
    # Teknik Secmeli havuzu (IsCompulsory=False, SuggestedSemester=0)
    ('IE352',0,False), ('IE354',0,False), ('IE356',0,False), ('IE358',0,False),
    ('IE362',0,False), ('IE364',0,False), ('IE372',0,False), ('IE374',0,False),
    ('IE376',0,False), ('IE380',0,False), ('IE382',0,False), ('IE384',0,False),
    ('IE386',0,False), ('IE388',0,False), ('EEE318',0,False), ('EEE320',0,False),
    ('ISE222',0,False), ('SENG264',0,False), ('ISE350',0,False),
    ('IE451',0,False), ('IE453',0,False), ('IE455',0,False), ('IE457',0,False),
    ('IE461',0,False), ('IE463',0,False), ('IE465',0,False), ('IE467',0,False),
    ('IE469',0,False), ('IE471',0,False), ('IE473',0,False), ('IE475',0,False),
    ('IE477',0,False), ('IE479',0,False), ('IE481',0,False),
    ('ISE211',0,False), ('ISE361',0,False), ('SENG231',0,False), ('CENG213',0,False),
    ('CENG375',0,False), ('CENG351',0,False),
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
