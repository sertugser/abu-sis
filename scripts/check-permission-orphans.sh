#!/bin/bash
# RBAC Phase 1 Sprint 5.2 — Permission Orphan Detection
# Codebase'de [RequirePermission(...)] ile kullanılan key'leri DB'deki Permissions tablosuyla karşılaştırır.
#
# Çıktı:
#   - Attribute'ta var ama DB'de YOK → eksik PermissionSeed entry (BACKEND 403 RİSKİ)
#   - DB'de var ama attribute'ta KULLANILMIYOR → orphan (cleanup adayı, riski yok)
#
# Kullanım:
#   bash scripts/check-permission-orphans.sh
#
# DB credentials default: postgres/12345 @ localhost:5432/StudentInfoSystem
# (appsettings.Development.json'dan)

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PG_BIN="/c/Program Files/PostgreSQL/18/bin"
USED_FILE=$(mktemp)
DB_FILE=$(mktemp)
trap "rm -f $USED_FILE $DB_FILE" EXIT

echo "[orphan-check] Codebase'den [RequirePermission(...)] key'leri çıkartılıyor..."
grep -roh 'RequirePermission("[^"]*")' "$PROJECT_ROOT/SIS.API/Controllers/" 2>/dev/null \
  | sed 's/RequirePermission("//;s/")//' \
  | sort -u > "$USED_FILE"

USED_COUNT=$(wc -l < "$USED_FILE")
echo "[orphan-check] Codebase'de $USED_COUNT unique key bulundu"

echo "[orphan-check] DB'den Permissions.Key listesi alınıyor..."
PGPASSWORD=12345 "$PG_BIN/psql.exe" -U postgres -h localhost -p 5432 -d StudentInfoSystem -t -A \
  -c 'SELECT "Key" FROM "Permissions" ORDER BY "Key";' 2>/dev/null \
  | grep -v '^$' \
  | sort -u > "$DB_FILE"

DB_COUNT=$(wc -l < "$DB_FILE")
echo "[orphan-check] DB'de $DB_COUNT permission key var"
echo ""

echo "═══════════════════════════════════════════════════════════════"
echo "❌ ATTRIBUTE'TA VAR, DB'DE YOK (eksik seed — kritik!)"
echo "═══════════════════════════════════════════════════════════════"
MISSING=$(comm -23 "$USED_FILE" "$DB_FILE")
if [ -z "$MISSING" ]; then
  echo "✅ (yok — tüm attribute key'leri DB'de mevcut)"
else
  # lookup.* hardcoded shortcut ile açık → DB seed gerekmez (Karar 7)
  REAL_MISSING=$(echo "$MISSING" | grep -v '^lookup\.' || true)
  LOOKUP_MISSING=$(echo "$MISSING" | grep '^lookup\.' || true)
  if [ -n "$LOOKUP_MISSING" ]; then
    echo "ℹ️  lookup.* keys (DB seed beklenmez, hardcoded shortcut OK):"
    echo "$LOOKUP_MISSING" | sed 's/^/    /'
  fi
  if [ -n "$REAL_MISSING" ]; then
    echo "❌ DB seed'e EKLENMESİ gereken key'ler:"
    echo "$REAL_MISSING" | sed 's/^/    /'
    echo ""
    echo "   → PermissionSeed.cs'i güncelle ve seed yeniden çalıştır."
  fi
fi
echo ""

echo "═══════════════════════════════════════════════════════════════"
echo "ℹ️  DB'DE VAR, ATTRIBUTE'TA KULLANILMIYOR (orphan — risksiz)"
echo "═══════════════════════════════════════════════════════════════"
ORPHAN=$(comm -13 "$USED_FILE" "$DB_FILE")
if [ -z "$ORPHAN" ]; then
  echo "✅ (yok — DB'deki tüm key'ler kullanımda)"
else
  ORPHAN_COUNT=$(echo "$ORPHAN" | wc -l)
  echo "$ORPHAN_COUNT orphan key (frontend menü filter'ı ya da SysAdmin shortcut için kullanılıyor olabilir)"
  echo "İlk 10:"
  echo "$ORPHAN" | head -10 | sed 's/^/    /'
fi
echo ""

echo "═══════════════════════════════════════════════════════════════"
echo "Özet: $USED_COUNT used | $DB_COUNT seeded"
echo "═══════════════════════════════════════════════════════════════"
