<?php
session_start();
if (!isset($_SESSION['user_id'])) {
    header("Location: log.php");
    exit;
}
?>
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>Создание образа — Выбор режима</title>
  <link rel="stylesheet" href="assets/css/style-choice.css">
</head>
<body>

  <div class="choice-container">
    <a href="profile.php" class="back-link">← назад</a>

    <h1 class="choice-title">Как вы хотите создать образ?</h1>
    <p class="choice-subtitle">Выберите удобный способ добавления аутфита</p>

    <div class="choice-grid">

      <!-- загрузить готовое фото по ссылке -->
      <a href="upload-outfit.php" class="choice-card">
        <div class="choice-icon">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
            <circle cx="8.5" cy="8.5" r="1.5"></circle>
            <polyline points="21 15 16 10 5 21"></polyline>
          </svg>
        </div>
        <div class="choice-card-title">Загрузить готовый образ</div>
        <div class="choice-card-desc">
          Вставьте ссылку на картинку готового образа, укажите название и описание
        </div>
      </a>

      <!-- собрать из одежды -->
      <a href="build.php" class="choice-card">
        <div class="choice-icon">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20.38 3.46L16 2a4 4 0 01-8 0L3.62 3.46a2 2 0 00-1.34 2.23l.58 3.47a1 1 0 00.99.84H6v10a2 2 0 002 2h8a2 2 0 002-2V10h2.15a1 1 0 00.99-.84l.58-3.47a2 2 0 00-1.34-2.23z"></path>
          </svg>
        </div>
        <div class="choice-card-title">Собрать из одежды</div>
        <div class="choice-card-desc">
          Отметь несколько сохранённых карточек - система соберёт из них один образ
        </div>
      </a>

    </div>
  </div>

</body>
</html>