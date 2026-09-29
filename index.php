<?php
session_start();
?>
<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <title>Главная</title>
    <link rel="stylesheet" href="assets/css/style.css">
</head>
<body>
    <h1>Fashion App</h1>

    <?php if (isset($_SESSION['user_id'])): ?>
        <p>Привет, <?= htmlspecialchars($_SESSION['username']) ?>!</p>
        <p>
            <a href="profile.php">Профиль</a> |
            <a href="wardrobe.php">Шкаф</a> |
            <a href="logout.php">Выйти</a>
        </p>
    <?php else: ?>
        <p>
            <a href="log.php">Войти</a> |
            <a href="reg.php">Регистрация</a>
        </p>
    <?php endif; ?>
</body>
</html>