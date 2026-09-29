<?php
session_start();
require __DIR__ . '/config/db.php';
require __DIR__ . '/classes/User.php';

$userModel = new User($pdo);
$error = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $username = trim($_POST['username'] ?? '');
    $email    = trim($_POST['email'] ?? '');
    $pass1    = $_POST['password'] ?? '';
    $pass2    = $_POST['confirm_password'] ?? '';

    if ($username === '' || $email === '' || $pass1 === '' || $pass2 === '') {
        $error = "Заполните все поля";
    } elseif (mb_strlen($username) < 3 || mb_strlen($username) > 30) {
        $error = "Логин от 3 до 30 символов";
    } elseif (mb_strlen($pass1) < 6) {
        $error = "Пароль минимум 6 символов";
    } elseif ($pass1 !== $pass2) {
        $error = "Пароли не совпадают";
    } elseif (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        $error = "Некорректный email";
    } elseif ($userModel->existsByUsernameOrEmail($username, $email)) {
        $error = "Логин или email уже занят";
    } else {
        $userId = $userModel->register($username, $email, $pass1);

        $_SESSION['user_id']  = $userId;
        $_SESSION['username'] = $username;

        header("Location: index.php");
        exit;
    }
}
?>
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Регистрация</title>
  <link rel="stylesheet" href="assets/css/style.css">
  <link rel="stylesheet" href="assets/css/style-reg.css">
</head>
<body>

  <div class="auth">
    <h2 class="wave-text">
      <span>Д</span><span>о</span><span>б</span><span>р</span><span>о</span><span>&nbsp;</span><span>п</span><span>о</span><span>ж</span><span>а</span><span>л</span><span>о</span><span>в</span><span>а</span><span>т</span><span>ь</span><span>!</span>
    </h2>

    <?php if ($error): ?>
      <p style="color:#fff; background:rgba(233,75,131,0.85); padding:10px; border-radius:8px; text-align:center; margin-bottom:14px;">
        <?= htmlspecialchars($error) ?>
      </p>
    <?php endif; ?>

    <form action="" method="POST">
      <input type="text"     name="username"         class="input-field" placeholder="Логин"         required>
      <input type="email"    name="email"            class="input-field" placeholder="Email"         required>
      <input type="password" name="password"         class="input-field" placeholder="Пароль"        required>
      <input type="password" name="confirm_password" class="input-field" placeholder="Повтор пароля" required>
      <button type="submit" class="submit-btn">Зарегистрироваться</button>
    </form>

    <div class="footer-link">
      <a href="log.php">Есть аккаунт?</a>
    </div>
  </div>

</body>
</html>