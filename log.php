<?php
session_start();
require __DIR__ . '/config/db.php';
require __DIR__ . '/classes/User.php';

$userModel = new User($pdo);
$error = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $login = trim($_POST['login'] ?? '');
    $pass  = $_POST['password'] ?? '';

    if ($login === '' || $pass === '') {
        $error = "Введите логин и пароль";
    } else {
        $user = $userModel->findByLoginOrEmail($login);

        if ($user && $userModel->verifyPassword($pass, $user['password_hash'])) {
            $_SESSION['user_id']  = $user['id'];
            $_SESSION['username'] = $user['username'];
            header("Location: index.php");
            exit;
        } else {
            $error = "Неверный логин или пароль";
        }
    }
}
?>
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Вход</title>
  <link rel="stylesheet" href="assets/css/style.css">
  <link rel="stylesheet" href="assets/css/style-log.css">
</head>
<body>

  <div class="log">
    <h2>Вход</h2>

    <?php if ($error): ?>
      <p style="color:#fff; background:rgba(233,75,131,0.85); padding:10px; border-radius:8px; text-align:center; margin-bottom:14px;">
        <?= htmlspecialchars($error) ?>
      </p>
    <?php endif; ?>

    <form action="" method="POST">
      <input type="text"     name="login"    class="input-field" placeholder="Логин"  required>
      <input type="password" name="password" class="input-field" placeholder="Пароль" required>
      <button type="submit" class="submit-btn">Войти</button>
    </form>

    <div class="footer-link">
      <a href="reg.php">Нет аккаунта.</a>
    </div>
  </div>

</body>
</html>