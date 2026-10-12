<?php
session_start();
if (!isset($_SESSION['user_id'])) {
    header("Location: log.php");
    exit;
}

$error   = $_SESSION['outfit_error'] ?? '';
$success = $_SESSION['outfit_success'] ?? '';
unset($_SESSION['outfit_error'], $_SESSION['outfit_success']);
?>
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>Добавить образ</title>
  <link rel="stylesheet" href="assets/css/style-upload.css">
</head>
<body>
  <div class="upload-container">
    <a href="choice.php" class="back-link">← назад</a>

    <div class="upload-card">

      <div class="upload-preview-box" id="previewBox">
        <img id="previewImg" class="preview-img" style="display:none;">
        <div id="uploadPlaceholder" style="text-align:center;">
          <div class="upload-text">Вставьте ссылку на картинку справа →</div>
        </div>
      </div>

      <form class="upload-form" method="POST" action="actions/save_outfit.php">
        <?php if ($error): ?>
          <p style="color:#d93838; background:#ffebeb; padding:10px; border-radius:10px; font-size:14px;">
            <?= htmlspecialchars($error) ?>
          </p>
        <?php endif; ?>
        <?php if ($success): ?>
          <p style="color:#43a047; background:#e8f5e9; padding:10px; border-radius:10px; font-size:14px;">
            <?= htmlspecialchars($success) ?>
          </p>
        <?php endif; ?>

        <div class="form-group">
          <label for="image_url">Ссылка на картинку *</label>
          <input type="url" id="image_url" name="image_url" class="form-input"
                 placeholder="https://i.pinimg.com/..." required>
        </div>

        <div class="form-group">
          <label for="title">Название</label>
          <input type="text" id="title" name="title" class="form-input"
                 placeholder="Например: Y2K образ" required>
        </div>

        <div class="form-group">
          <label for="description">Описание</label>
          <textarea id="description" name="description" class="form-textarea" rows="3"
                    placeholder="Короткое описание"></textarea>
        </div>

        <button type="submit" class="btn-submit">Сохранить образ</button>
      </form>

    </div>
  </div>

  <script>
    const imageUrlInput = document.getElementById('image_url');
    const previewImg = document.getElementById('previewImg');
    const uploadPlaceholder = document.getElementById('uploadPlaceholder');

    imageUrlInput.addEventListener('input', () => {
      const url = imageUrlInput.value.trim();
      if (url && /^https?:\/\//.test(url)) {
        previewImg.src = url;
        previewImg.style.display = 'block';
        uploadPlaceholder.style.display = 'none';
        previewImg.onerror = () => {
          previewImg.style.display = 'none';
          uploadPlaceholder.style.display = 'block';
          uploadPlaceholder.innerHTML = '<div class="upload-text" style="color:#d93838;">Не удалось загрузить картинку. Проверь ссылку.</div>';
        };
      } else {
        previewImg.style.display = 'none';
        uploadPlaceholder.style.display = 'block';
        uploadPlaceholder.innerHTML = '<div class="upload-text">Вставьте ссылку на картинку справа →</div>';
      }
    });
  </script>

</body>
</html>