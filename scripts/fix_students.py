import psycopg2, sys, io, random
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
conn = psycopg2.connect(host='localhost', port=5432, dbname='StudentInfoSystem', user='postgres', password='12345', client_encoding='UTF8')
cur = conn.cursor()
random.seed(2024)

grade_points = {0: 4.0, 1: 3.5, 2: 3.0, 3: 2.5, 4: 2.0, 5: 1.5, 6: 1.0, 7: 0.0}
grade_names = {0:'AA', 1:'BA', 2:'BB', 3:'CB', 4:'CC', 5:'DC', 6:'DD', 7:'FF'}

# =========================================================================
# STUDENT CONFIG
# (id, new_snum, entry_year, target_cgpa, is_retained)
#
# Distribution:
#   1st year (2025): Burak(BM-EN), Kaan(EM), Irem(YM)
#   2nd year (2024): Emre(BM-EN), Kenan(BM-TR), Orkun*(BSM), Selin(EEM), Can*(YM)
#   3rd year (2023): Zeynep(BM-TR), Arthur(BSM), Deniz*(EM), Ege*(EEM), Yusuf(YM)
#   4th year (2022): Ayse(BM-EN), Elif*(BM-TR), Sergen(BSM), Ceren(EM), Baran(EEM)
#   * = sinifta kalmis (retained, CGPA < 1.70)
# =========================================================================
student_config = [
    # BM Ingilizce (Prog 1)
    (1,  '2201001', 2022, 3.20, False),   # Ayse Yilmaz - 4th year, basarili
    (2,  '2401002', 2024, 1.95, False),   # Emre Kaya - 2nd year, ortanin alti
    (6,  '2501003', 2025, 2.50, False),   # Burak Demir - 1st year, yeni basladi

    # BM Turkce (Prog 2)
    (7,  '2302001', 2023, 2.80, False),   # Zeynep Arslan - 3rd year, iyi
    (8,  '2402002', 2024, 1.78, False),   # Kenan Yildiz - 2nd year, dusuk
    (9,  '2202003', 2022, 1.45, True),    # Elif Ozdemir - 4th year, SINIFTA KALDI

    # BSM (Prog 3)
    (10, '2203001', 2022, 3.45, False),   # Sergen Yalcin - 4th year, basarili
    (11, '2303002', 2023, 2.10, False),   # Arthur Morgan - 3rd year, orta
    (12, '2403003', 2024, 1.55, True),    # Orkun Kokcu - 2nd year, SINIFTA KALDI

    # Endustri (Prog 4)
    (13, '2204001', 2022, 2.91, False),   # Ceren Yildiz - 4th year, iyi
    (14, '2504002', 2025, 2.40, False),   # Kaan Celik - 1st year, yeni basladi
    (15, '2304003', 2023, 1.38, True),    # Deniz Kurt - 3rd year, SINIFTA KALDI

    # EEM (Prog 5)
    (16, '2205001', 2022, 3.50, False),   # Baran Aksoy - 4th year, basarili
    (17, '2405002', 2024, 2.15, False),   # Selin Dogan - 2nd year, orta
    (18, '2305003', 2023, 1.60, True),    # Ege Koc - 3rd year, SINIFTA KALDI

    # YM (Prog 6)
    (19, '2306001', 2023, 2.56, False),   # Yusuf Aydin - 3rd year, iyi
    (20, '2506002', 2025, 3.00, False),   # Irem Tas - 1st year, iyi baslangic
    (21, '2406003', 2024, 1.65, True),    # Can Polat - 2nd year, SINIFTA KALDI
]

# =========================================================================
# PART 1: Temizlik
# =========================================================================
print('=== PART 1: Temizlik ===')
cur.execute('DELETE FROM "CourseRegistrations"')
cur.execute('DELETE FROM "CourseSections"')
print('  CourseRegistrations ve CourseSections silindi')
conn.commit()

# =========================================================================
# PART 2: Ogrenci bilgilerini guncelle
# =========================================================================
print('\n=== PART 2: Ogrenci bilgileri guncelleniyor ===')

# Once hepsini gecici numara yap (unique constraint)
for sid, new_snum, *_ in student_config:
    cur.execute('UPDATE "Students" SET "StudentNumber" = %s, "NationalId" = %s WHERE "Id" = %s',
        (f'TEMP_{sid}', f'TEMP_{sid}', sid))
conn.commit()

# Simdi gercek degerleri ata
for sid, new_snum, entry_year, target_cgpa, retained in student_config:
    national_id = f'1{new_snum}00000'
    enrollment_date = f'{entry_year}-09-15'

    cur.execute('UPDATE "Students" SET "StudentNumber" = %s, "NationalId" = %s WHERE "Id" = %s',
        (new_snum, national_id, sid))
    cur.execute('UPDATE "StudentPrograms" SET "EnrollmentDate" = %s WHERE "StudentId" = %s AND "IsActive" = true',
        (enrollment_date, sid))

    yr = {2022: '4. sinif', 2023: '3. sinif', 2024: '2. sinif', 2025: '1. sinif'}
    ret = ' [SINIFTA KALDI]' if retained else ''
    print(f'  Student {sid}: {new_snum} | {yr.get(entry_year, "?")} | hedef={target_cgpa}{ret}')

conn.commit()

# =========================================================================
# PART 3: Veri toplama
# =========================================================================
print('\n=== PART 3: Veri toplama ===')

# Donemler
cur.execute('SELECT "Id", "Year", "Semester" FROM "AcademicTerms" ORDER BY "Year", "Semester"')
term_map = {}
for r in cur.fetchall():
    term_map[(r[1], r[2])] = r[0]
print(f'  {len(term_map)} donem')
active_term_id = term_map[(2025, 2)]

# Mufredat dersleri
curricula_courses = {}
cur.execute('''SELECT cc."CurriculumId", cc."SuggestedSemester", cc."CourseId", c."Ects"
FROM "CurriculumCourses" cc JOIN "Courses" c ON cc."CourseId" = c."Id"
WHERE cc."IsCompulsory" = true AND cc."SuggestedSemester" > 0
ORDER BY cc."CurriculumId", cc."SuggestedSemester"''')
for r in cur.fetchall():
    cid, sem, course_id, ects = r
    curricula_courses.setdefault(cid, {}).setdefault(sem, []).append((course_id, int(ects)))

# Ogrenci -> Mufredat/Program
cur.execute('''SELECT s."Id", sp."CurriculumId", sp."ProgramId"
FROM "Students" s JOIN "StudentPrograms" sp ON s."Id" = sp."StudentId" AND sp."IsActive" = true''')
student_curriculum = {}
student_program = {}
for r in cur.fetchall():
    student_curriculum[r[0]] = r[1]
    student_program[r[0]] = r[2]

# Program -> Department
cur.execute('SELECT "Id", "DepartmentId" FROM "AcademicPrograms"')
prog_dept = {r[0]: r[1] for r in cur.fetchall()}

# Ogretmenler
cur.execute('SELECT "Id", "DepartmentId" FROM "Instructors" ORDER BY "Id"')
dept_instructors = {}
for r in cur.fetchall():
    dept_instructors.setdefault(r[1], []).append(r[0])

# Derslikler
cur.execute('SELECT "Id", "Capacity", "Type" FROM "Classrooms" ORDER BY "Id"')
classrooms = cur.fetchall()
lecture_rooms = [r for r in classrooms if r[2] == 0]
lab_rooms = [r for r in classrooms if r[2] == 1]

# =========================================================================
# PART 4: Not atama fonksiyonlari
# =========================================================================

def pick_grade(target_cgpa, running_cgpa, credits_done, rng):
    """Hedef CGPA'ya gore rastgele not sec, running GPA'ya gore ayarla"""
    # Temel agirliklar (kalibre edilmis E degerleri)
    if target_cgpa >= 3.3:
        weights = [35, 25, 20, 10, 6, 2, 1, 1]    # E~3.35
    elif target_cgpa >= 2.8:
        weights = [15, 22, 25, 18, 10, 5, 3, 2]    # E~2.88
    elif target_cgpa >= 2.3:
        weights = [8, 14, 20, 22, 20, 10, 4, 2]    # E~2.52
    elif target_cgpa >= 1.9:
        weights = [3, 6, 12, 18, 25, 20, 10, 6]    # E~2.02
    elif target_cgpa >= 1.7:
        weights = [2, 4, 8, 14, 25, 24, 16, 7]     # E~1.82
    elif target_cgpa >= 1.4:
        weights = [0, 2, 4, 8, 16, 22, 26, 22]     # E~1.36
    else:
        weights = [0, 1, 2, 5, 10, 16, 28, 38]     # E~1.04

    # Running GPA'ya gore ayarla (cok sapma varsa duzelt)
    if credits_done >= 10:
        diff = running_cgpa - target_cgpa
        if diff > 0.3:  # GPA cok yuksek, dusur
            # Ust notlarin agirligini azalt, alt notlari artir
            for i in range(3):
                shift = int(weights[i] * 0.4)
                weights[i] -= shift
                weights[min(i + 3, 7)] += shift
        elif diff < -0.3:  # GPA cok dusuk, yukselt
            for i in range(4, 8):
                shift = int(weights[i] * 0.4)
                weights[i] -= shift
                weights[max(i - 3, 0)] += shift

    return rng.choices([0,1,2,3,4,5,6,7], weights=weights, k=1)[0]

def pick_retake_grade(target_cgpa, rng):
    """Tekrar alinan ders icin not - biraz daha iyi sans"""
    if target_cgpa >= 1.7:
        weights = [2, 5, 12, 18, 28, 18, 12, 5]
    elif target_cgpa >= 1.4:
        weights = [0, 2, 5, 10, 20, 25, 20, 18]
    else:
        weights = [0, 1, 3, 6, 14, 18, 26, 32]
    return rng.choices([0,1,2,3,4,5,6,7], weights=weights, k=1)[0]

def correct_cgpa(course_grades, target_cgpa, is_retained):
    """Not-retained ogrencilerin GPA'sini 1.70 uzerine cikar, hedef GPA'ya yaklastir"""
    if not course_grades:
        return
    total_qp = sum(grade_points[g] * e for _, (e, g) in course_grades.items())
    total_ects = sum(e for _, (e, g) in course_grades.items())
    current_cgpa = total_qp / total_ects if total_ects > 0 else 0

    if is_retained:
        return  # Sinifta kalan ogrenciler icin duzeltme yapma

    # Non-retained ogrenci: GPA < 1.70 ise bazi notlari yukselt
    min_gpa = max(1.70, target_cgpa - 0.25)
    if current_cgpa >= min_gpa:
        return

    # En dusuk notlu dersleri bul ve yukselt
    sorted_courses = sorted(course_grades.items(), key=lambda x: x[1][1], reverse=True)  # en kotu not en sonda
    for cid, (ects, grade) in sorted_courses:
        if current_cgpa >= min_gpa:
            break
        if grade >= 5:  # DC, DD, FF
            new_grade = max(0, grade - 2)  # 2 kademe yukselt
            old_qp = grade_points[grade] * ects
            new_qp = grade_points[new_grade] * ects
            total_qp += (new_qp - old_qp)
            current_cgpa = total_qp / total_ects
            course_grades[cid] = (ects, new_grade)

# =========================================================================
# PART 5: Section olusturma
# =========================================================================

section_cache = {}
section_counter = [0]

def get_or_create_section(course_id, term_id, dept_id):
    key = (course_id, term_id)
    if key in section_cache:
        return section_cache[key]

    instructors = dept_instructors.get(dept_id, dept_instructors.get(1, [2]))
    instructor_id = instructors[section_counter[0] % len(instructors)]
    room = lecture_rooms[section_counter[0] % len(lecture_rooms)]

    cur.execute('''INSERT INTO "CourseSections" ("CourseId","TermId","InstructorId","Capacity","EnrolledCount","ClassroomId","IsActive","ScheduleJson")
        VALUES (%s,%s,%s,%s,%s,%s,%s,%s) RETURNING "Id"''',
        (course_id, term_id, instructor_id, room[1], 0, room[0], True, '[]'))
    sec_id = cur.fetchone()[0]
    section_cache[key] = sec_id
    section_counter[0] += 1
    return sec_id

def sem_to_term(entry_year, semester_num):
    """Ogrencinin n. donemini akademik donem ID'sine cevir"""
    year_offset = (semester_num - 1) // 2
    sem_type = 1 if semester_num % 2 == 1 else 2
    year = entry_year + year_offset
    return term_map.get((year, sem_type))

# =========================================================================
# PART 6: Gecmis donem kayitlari
# =========================================================================
print('\n=== PART 6: Kayitlar olusturuluyor ===')

total_registrations = 0

for sid, new_snum, entry_year, target_cgpa, is_retained in student_config:
    curr_id = student_curriculum.get(sid)
    prog_id = student_program.get(sid)
    dept_id = prog_dept.get(prog_id, 1)

    if curr_id is None or curr_id not in curricula_courses:
        print(f'  Student {sid}: Mufredat bulunamadi, atlaniyor')
        continue

    rng = random.Random(sid * 1000 + 2024)

    # Kac takvim donemi tamamlanmis
    completed = 0
    for s in range(1, 20):
        tid = sem_to_term(entry_year, s)
        if tid == active_term_id:
            break
        if tid is not None:
            completed += 1

    # Not takibi: course_id -> (ects, latest_grade)
    course_grades = {}
    ff_courses = []  # (course_id, ects) - tekrar alinacak dersler
    curriculum_semester = 0  # en son tamamlanan mufredat donemi
    student_regs = 0
    cgpa_running = 4.0

    for cal_sem in range(1, completed + 1):
        tid = sem_to_term(entry_year, cal_sem)
        if tid is None or tid == active_term_id:
            continue

        # 1. Tekrar dersleri (FF olan)
        still_ff = []
        for course_id, ects in ff_courses:
            grade = pick_retake_grade(target_cgpa, rng)
            sec_id = get_or_create_section(course_id, tid, dept_id)
            cur.execute('''INSERT INTO "CourseRegistrations"
                ("StudentId","CourseSectionId","Status","Grade","IsRepeated",
                 "MandatoryAttendance","CourseWithdrawalCount","CreatedAt","UpdatedAt")
                VALUES (%s,%s,%s,%s,%s,%s,%s,NOW(),NOW())''',
                (sid, sec_id, 2, grade, True, False, 0))
            course_grades[course_id] = (ects, grade)
            if grade == 7:
                still_ff.append((course_id, ects))
            student_regs += 1

        ff_courses = still_ff

        # 2. Guncel GPA hesapla
        if course_grades:
            total_qp = sum(grade_points[g] * e for _, (e, g) in course_grades.items())
            total_ects = sum(e for _, (e, g) in course_grades.items())
            cgpa_running = total_qp / total_ects if total_ects > 0 else 4.0

        # 3. Yeni ders alabilir mi?
        # Sinifta kalan ogrenci: GPA < 1.70 ise ust donem dersi alamaz
        # Ama FF dersi kalmadiysa ilerlemek zorunda (bos donem olmaz)
        can_advance = (
            cal_sem == 1 or                    # ilk donem her zaman
            not is_retained or                  # sinifta kalmayan her zaman ilerler
            cgpa_running >= 1.70 or             # GPA yeterliyse ilerler
            len(ff_courses) == 0                # FF dersi kalmadiysa ilerlemek zorunda
        )

        if can_advance:
            next_sem = curriculum_semester + 1
            courses = curricula_courses[curr_id].get(next_sem, [])
            if courses:  # mufredat donemi varsa
                curriculum_semester = next_sem
                for course_id, ects in courses:
                    credits_done = sum(e for _, (e, g) in course_grades.items())
                    grade = pick_grade(target_cgpa, cgpa_running, credits_done, rng)
                    sec_id = get_or_create_section(course_id, tid, dept_id)
                    cur.execute('''INSERT INTO "CourseRegistrations"
                        ("StudentId","CourseSectionId","Status","Grade","IsRepeated",
                         "MandatoryAttendance","CourseWithdrawalCount","CreatedAt","UpdatedAt")
                        VALUES (%s,%s,%s,%s,%s,%s,%s,NOW(),NOW())''',
                        (sid, sec_id, 2, grade, False, False, 0))
                    course_grades[course_id] = (ects, grade)
                    if grade == 7:
                        ff_courses.append((course_id, ects))
                    student_regs += 1
                    # Running GPA guncelle
                    total_qp = sum(grade_points[g] * e for _, (e, g) in course_grades.items())
                    total_ects = sum(e for _, (e, g) in course_grades.items())
                    cgpa_running = total_qp / total_ects if total_ects > 0 else 4.0

    # GPA duzeltme (non-retained ogrencileri 1.70 uzerine cikar)
    correct_cgpa(course_grades, target_cgpa, is_retained)

    # Final CGPA hesapla (gercek notlardan)
    if course_grades:
        total_qp = sum(grade_points[g] * e for _, (e, g) in course_grades.items())
        total_ects = sum(e for _, (e, g) in course_grades.items())
        final_cgpa = round(total_qp / total_ects, 2) if total_ects > 0 else 0.0
    else:
        final_cgpa = 0.0

    # Ogrenciyi guncelle
    cur.execute('UPDATE "Students" SET "Cgpa" = %s, "Sgpa" = %s, "CompletedSemesters" = %s WHERE "Id" = %s',
        (final_cgpa, final_cgpa, completed, sid))

    ff_count = sum(1 for _, (e, g) in course_grades.items() if g == 7)
    total_registrations += student_regs
    ret_str = f' *** SINIFTA KALDI (mufredat sem {curriculum_semester})' if is_retained and final_cgpa < 1.70 else ''
    print(f'  Student {sid} ({new_snum}): {completed} donem, {student_regs} kayit, '
          f'mufredat={curriculum_semester}. sem, CGPA={final_cgpa:.2f}, {ff_count} FF{ret_str}')

print(f'\n  Toplam: {total_registrations} kayit, {len(section_cache)} section')
conn.commit()

# =========================================================================
# PART 7: Aktif donem sectionlari
# =========================================================================
print('\n=== PART 7: Aktif donem sectionlari ===')

for sid, new_snum, entry_year, target_cgpa, is_retained in student_config:
    curr_id = student_curriculum.get(sid)
    prog_id = student_program.get(sid)
    dept_id = prog_dept.get(prog_id, 1)

    if curr_id is None or curr_id not in curricula_courses:
        continue

    # Bu ogrencinin simdiki mufredat donemini bul
    # (Basit: completed_calendar_semesters + 1, ama sinifta kalanlar icin farkli)
    # Mufredat donemini yeniden hesapla
    rng2 = random.Random(sid * 1000 + 2024)
    completed = 0
    for s in range(1, 20):
        tid = sem_to_term(entry_year, s)
        if tid == active_term_id:
            break
        if tid is not None:
            completed += 1

    # Mufredat ilerleme simule et (kisa versiyon)
    curriculum_semester = 0
    ff_count = 0
    cg = {}
    cgpa_r = 4.0

    for cal_sem in range(1, completed + 1):
        tid = sem_to_term(entry_year, cal_sem)
        if tid is None or tid == active_term_id:
            continue

        # FF tekrar
        new_ff = 0
        for _ in range(ff_count):
            g = pick_retake_grade(target_cgpa, rng2)
            if g == 7:
                new_ff += 1

        # GPA hesapla (kaba)
        if cg:
            total_qp = sum(grade_points[g] * e for _, (e, g) in cg.items())
            total_ects = sum(e for _, (e, g) in cg.items())
            cgpa_r = total_qp / total_ects if total_ects > 0 else 4.0

        can_advance = (cal_sem == 1 or not is_retained or cgpa_r >= 1.70 or ff_count == 0)

        if can_advance:
            next_sem = curriculum_semester + 1
            courses = curricula_courses[curr_id].get(next_sem, [])
            if courses:
                curriculum_semester = next_sem
                ff_count = new_ff
                for course_id, ects in courses:
                    credits_done = sum(e for _, (e, _g) in cg.items())
                    g = pick_grade(target_cgpa, cgpa_r, credits_done, rng2)
                    cg[course_id] = (ects, g)
                    if g == 7:
                        ff_count += 1
            else:
                ff_count = new_ff
        else:
            ff_count = new_ff

    current_sem = curriculum_semester + 1

    # Aktif donem dersleri
    courses = curricula_courses[curr_id].get(current_sem, [])
    for course_id, ects in courses:
        get_or_create_section(course_id, active_term_id, dept_id)

print(f'  Aktif donem section kontrolu tamamlandi')

# =========================================================================
# PART 8: Danisman atamalari
# =========================================================================
print('\n=== PART 8: Danisman atamalari ===')

for sid, new_snum, entry_year, target_cgpa, is_retained in student_config:
    prog_id = student_program.get(sid)
    dept_id = prog_dept.get(prog_id, 1)

    # BSM (dept 3) ve YM (dept 6) BM kadrosunu paylasir
    if dept_id in (2, 3, 6):
        advisor_dept = 1
    else:
        advisor_dept = dept_id

    instructors = dept_instructors.get(advisor_dept, dept_instructors.get(1, [2]))
    advisor_id = instructors[0]

    cur.execute('UPDATE "Students" SET "AdvisorId" = %s WHERE "Id" = %s', (advisor_id, sid))

print('  Danismanlar atandi')
conn.commit()

# =========================================================================
# FINAL OZET
# =========================================================================
print('\n=== FINAL OZET ===')
cur.execute('SELECT COUNT(*) FROM "CourseSections"')
print(f'  Toplam section: {cur.fetchone()[0]}')
cur.execute('SELECT COUNT(*) FROM "CourseRegistrations"')
print(f'  Toplam kayit: {cur.fetchone()[0]}')

cur.execute('''SELECT s."Id", u."FirstName", u."LastName", s."StudentNumber", s."CompletedSemesters", s."Cgpa", s."AdvisorId",
    (SELECT COUNT(*) FROM "CourseRegistrations" cr WHERE cr."StudentId" = s."Id") as reg_count,
    (SELECT COUNT(*) FROM "CourseRegistrations" cr WHERE cr."StudentId" = s."Id" AND cr."Grade" = 7) as ff_count,
    (SELECT COUNT(*) FROM "CourseRegistrations" cr WHERE cr."StudentId" = s."Id" AND cr."IsRepeated" = true) as retake_count
FROM "Students" s JOIN "AspNetUsers" u ON s."UserId" = u."Id"
ORDER BY s."Id"''')
print('\n  Ogrenci detaylari:')
for r in cur.fetchall():
    held = ' *** SINIFTA KALDI' if float(r[5]) < 1.70 else ''
    print(f'    Student {r[0]} | {r[1]} {r[2]} | {r[3]} | {r[4]} donem | CGPA={r[5]} | '
          f'Danisman={r[6]} | {r[7]} kayit | {r[8]} FF | {r[9]} tekrar{held}')

# Not dagilimi
cur.execute('''SELECT cr."Grade", COUNT(*)
FROM "CourseRegistrations" cr
GROUP BY cr."Grade" ORDER BY cr."Grade"''')
print('\n  Not dagilimi:')
for r in cur.fetchall():
    print(f'    {grade_names[r[0]]}: {r[1]} kayit')

# Donem bazli sectionlar
cur.execute('''SELECT cs."TermId", t."Name", COUNT(*)
FROM "CourseSections" cs JOIN "AcademicTerms" t ON cs."TermId" = t."Id"
GROUP BY cs."TermId", t."Name" ORDER BY MIN(t."Year"), MIN(t."Semester")''')
print('\n  Donem bazli section sayilari:')
for r in cur.fetchall():
    aktif = ' <<< AKTIF' if r[0] == active_term_id else ''
    print(f'    {r[1]}: {r[2]} section{aktif}')

conn.close()
print('\nTamamlandi!')
