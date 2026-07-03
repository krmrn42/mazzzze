# REQ-0018-localization — Реализация (Design / HOW)

> **Фича:** REQ-0018-localization · **Тип документа:** реализация (HOW, набросок — фича не реализована)
> **Глобальный контекст:** [TECH_SPEC.md](../TECH_SPEC.md).

Фича не реализована. Ниже — предварительный технический набросок; уточняется по мере написания кода.

---

## Технология

Проект на C# / Godot 4.6. Используется встроенная система переводов Godot:

- Источник — CSV; при импорте Godot компилирует по одному ресурсу `Translation` на каждую языковую колонку.
- `TranslationServer` хранит зарегистрированные переводы и текущую локаль.
- Получение строки: `TranslationServer.Translate("KEY")` (канонический способ; возвращает строку текущей локали). В GDScript — `tr("KEY")`.

## Переводные ресурсы (F-35)

- Папка: `res://translations/`.
- Источник: `res://translations/strings.csv`. Первая колонка — `key`, далее колонки `ru`, `en`:

  ```csv
  key,ru,en
  INV_BACKPACK_COUNT,"Рюкзак  {0}/{1}","Backpack  {0}/{1}"
  INV_DROP_HINT,"Shift/G — выбросить","Shift/G — drop"
  ```

  (Будущие ключи добавляются строками в тот же файл.)

- Импорт: Godot при импорте CSV генерирует `strings.ru.translation`, `strings.en.translation` рядом с CSV. Они регистрируются в `internationalization/locale/translations` (см. ниже).
- Базовый язык — `en`; перевод для `en` обязан быть полным (ключи абстрактные — показ ключа недопустим).

## Секция project.godot (F-37)

Сейчас секции `[internationalization]` нет. Добавить:

```ini
[internationalization]

locale/translations=PackedStringArray("res://translations/strings.en.translation", "res://translations/strings.ru.translation")
locale/fallback="en"
; locale/test=""   ; для разработки можно задать, напр. "ru" — перекрывает локаль ОС. В релизе пусто.
```

Порядок записей в `locale/translations` не важен: Godot выбирает перевод по совпадению локали (через `compare_locales`), а не по порядку. `locale/test` — официальная проектная настройка для принудительного языка (используется тестировщиками; в релизе пуста).

> Если редактор при импорте регистрирует CSV иначе (зависит от пресета импорта) — финальный вид строки `locale/translations` уточняется на месте; суть (зарегистрировать скомпилированные переводы `en`+`ru`) неизменна.

## Выбор языка при запуске (F-36)

Godot по умолчанию использует локаль ОС, если `locale/test` пуст, — это уже даёт требуемое «язык = локаль устройства». Чтобы поведение было явным, устойчивым и поддерживало нормализацию региона, добавим минимальный bootstrap.

Вариант — autoload (напр. `src/LocaleBootstrap.cs`, зарегистрирован в `[autoload]`), в `_Ready`:

```csharp
public override void _Ready()
{
    string loc = OS.GetLocale();          // "ru_RU", "en_US", ...
    TranslationServer.SetLocale(loc);     // fallback-цепочка Godot подберёт перевод
}
```

Autoload запускается до первого UI (отрисовка HUD идёт в `_Process`/`_Draw` после `_Ready`), поэтому локаль успеет встать.

Цепочка fallback (встроена в Godot): полная локаль (`en_US`) → язык без региона (`en`) → `locale/fallback` (`en`). Благодаря полноте `en` сырой ключ не покажется никогда.

Тестовый язык: `locale/test="ru"` (или `"en"`) форсирует язык, перекрывая autoload/ОС. **В релизе пусто.**

## Использование в коде (F-38)

- Видимый текст — через `TranslationServer.Translate("KEY")`.
- Подстановка значений — `string.Format` с индексными плейсхолдерами (порядок слов может меняться в переводе):

  ```csharp
  // было:  $"Рюкзак  {_inv.Count}/{Inventory.Capacity}"
  // стало:
  string tmpl = TranslationServer.Translate("INV_BACKPACK_COUNT");
  DrawString(font, pos, string.Format(tmpl, _inv.Count, Inventory.Capacity), ...);

  // было:  "Shift/G — выбросить"
  // стало:
  DrawString(font, pos, TranslationServer.Translate("INV_DROP_HINT"), ...);
  ```

- `GD.Print` не трогаем — отладочные строки остаются как есть (для разработчиков).

## Миграция текущих строк (один проход при реализации)

1. Создать `res://translations/strings.csv` с ключами `INV_BACKPACK_COUNT`, `INV_DROP_HINT` и колонками `ru`, `en`.
2. Добавить секцию `[internationalization]` в `project.godot`.
3. Добавить autoload `LocaleBootstrap` (`src/LocaleBootstrap.cs`) + регистрацию в `[autoload]`.
4. В `src/InventoryHud.cs` заменить 2 строковых литерала на вызовы перевода (как выше).
5. Импортировать CSV в редакторе Godot → убедиться, что `.translation`-файлы сгенерированы.
6. Проверить: запуск при `LC_ALL`/локали ОС = `ru` → «Рюкзак»; при `en` → «Backpack»; при отсутствии перевода (напр. `fr`) → «Backpack» (fallback на `en`).

## Открытые вопросы

- Autoload vs. установка локали в корневом узле `Main._Ready` — autoload чище (не зависит от сцены).
- Нужен ли редакторский скрипт проверки полноты CSV (все ключи есть во всех языках; `en` обязателен) — желательно, но опционально.
- Префиксация ключей по доменам (`INV_*`, `ITEM_*`, …) — закрепить в соглашении (см. [04-code-usage.md](./04-code-usage.md)).

## Границы

- Без ручного переключения языка игроком (язык = устройство).
- Без сохранения языка между сессиями.
- Без локализации отладочных `GD.Print`.
- Без перевода ассетов (текстур/аудио) через `translation_remaps` — пока только строки.
