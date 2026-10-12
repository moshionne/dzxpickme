<?php
session_start();
if (!isset($_SESSION['user_id'])) {
    header("Location: log.php");
    exit;
}
require __DIR__ . '/config/db.php';
require __DIR__ . '/classes/ClothingItem.php';

$itemModel = new ClothingItem($pdo);
$items = $itemModel->getAllByUser($_SESSION['user_id']);
?>
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>Конструктор образа</title>
  <link rel="stylesheet" href="assets/css/style-builder.css">
</head>
<body>

<header class="top-bar">
  <a href="choice.php" class="back-btn">← назад</a>
  <h3>конструктор образа</h3>
  <button class="btn-save-outfit" id="saveBuiltBtn" disabled>сохранить образ</button>
</header>

<div class="workspace">

  <!-- ЛЕВАЯ ПАНЕЛЬ: Сохранённые карточки -->
  <aside class="sidebar-left">
    <div class="tab-buttons">
      <button class="tab-btn active" data-tab="saved">Мои сохранёнки</button>
    </div>

    <div class="tab-content active" id="tab-saved">
      <?php if (empty($items)): ?>
        <div class="hint-box">
          У тебя нет сохранёнок. Зайди в <a href="main.php" style="color:#e94b83;">ленту</a> и сохрани что-нибудь.
        </div>
      <?php else: ?>
        <div class="clothes-grid" id="savedGrid">
          <?php foreach ($items as $item): ?>
            <div class="draggable-item" data-url="<?= htmlspecialchars($item['image_url']) ?>">
              <img src="<?= htmlspecialchars($item['image_url']) ?>" alt="" loading="lazy">
            </div>
          <?php endforeach; ?>
        </div>
      <?php endif; ?>
    </div>
  </aside>

  <!-- холст: превью -->
  <main class="canvas-area">
    <canvas id="buildCanvas" class="canvas-board" width="600" height="800"></canvas>
  </main>

  <aside class="sidebar-right">

    <div class="tool-group">
      <span class="tool-title">Название образа</span>
      <input type="text" id="outfitTitle" class="tool-btn"
             placeholder="Название" maxlength="200"
             style="text-align:left; cursor:text;">
    </div>

    <div class="tool-group">
      <span class="tool-title">Описание</span>
      <textarea id="outfitDesc" class="tool-btn" rows="3"
                placeholder="Описание (необязательно)" maxlength="500"
                style="text-align:left; cursor:text; resize:none; font-family:inherit;"></textarea>
    </div>

    <div class="tool-group">
      <span class="tool-title">Выбрано</span>
      <div class="hint-box">
        Выбрано: <strong id="selectedCount" style="color:#e94b83;">0</strong> / 6<br>
        Кликни по карточке слева, чтобы добавить или убрать.
      </div>
    </div>

    <div class="tool-group">
      <span class="tool-title">Управление</span>
      <button class="tool-btn" id="clearSelectionBtn">✕ Очистить выбор</button>
    </div>

  </aside>

</div>

<script>
const canvas = document.getElementById('buildCanvas');
const ctx = canvas.getContext('2d');
const selectedCards = new Set();
const savedGrid = document.getElementById('savedGrid');
const saveBtn = document.getElementById('saveBuiltBtn');
const selectedCount = document.getElementById('selectedCount');
const clearBtn = document.getElementById('clearSelectionBtn');

// клик по карточке — toggle выбора
if (savedGrid) {
  savedGrid.addEventListener('click', (e) => {
    const card = e.target.closest('.draggable-item');
    if (!card) return;

    const url = card.dataset.url;
    if (selectedCards.has(url)) {
      selectedCards.delete(url);
      card.classList.remove('selected');
      card.style.borderColor = '';
      card.style.boxShadow = '';
    } else {
      if (selectedCards.size >= 6) {
        alert('Максимум 6 карточек для одного образа');
        return;
      }
      selectedCards.add(url);
      card.classList.add('selected');
      card.style.borderColor = '#e94b83';
      card.style.boxShadow = '0 0 0 3px rgba(233,75,131,0.3)';
    }
    selectedCount.textContent = selectedCards.size;
    renderCanvas();
  });
}

// очист всё
clearBtn.addEventListener('click', () => {
  selectedCards.clear();
  document.querySelectorAll('.draggable-item').forEach(c => {
    c.classList.remove('selected');
    c.style.borderColor = '';
    c.style.boxShadow = '';
  });
  selectedCount.textContent = '0';
  renderCanvas();
});

// рисуется превью на canvas
function renderCanvas() {
  const urls = Array.from(selectedCards);
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // общ фон canvas — белый
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  if (urls.length === 0) {
    ctx.fillStyle = '#a89098';
    ctx.font = '22px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText('Выбери карточки слева', canvas.width / 2, canvas.height / 2);
    saveBtn.disabled = true;
    return;
  }

  // сетка
  let cols, rows;
  if (urls.length === 1) { cols = 1; rows = 1; }
  else if (urls.length === 2) { cols = 2; rows = 1; }
  else if (urls.length <= 4) { cols = 2; rows = 2; }
  else { cols = 3; rows = 2; }

  const cellW = canvas.width / cols;
  const cellH = canvas.height / rows;
  const pad = 10;  // отступ внутри ячейки

  let loaded = 0;

  urls.forEach((url, i) => {
    const col = i % cols;
    const row = Math.floor(i / cols);

    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      const availW = cellW - pad * 2;
      const availH = cellH - pad * 2;

      // contain — картинка целиком влезает, без обрезки
      const scale = Math.min(availW / img.width, availH / img.height);
      const w = img.width * scale;
      const h = img.height * scale;

      const x = col * cellW + (cellW - w) / 2;
      const y = row * cellH + (cellH - h) / 2;

      // Рисуем без clip — картинка целиком
      ctx.drawImage(img, x, y, w, h);

      loaded++;
      if (loaded === urls.length) saveBtn.disabled = false;
    };
    img.onerror = () => {
      loaded++;
      if (loaded === urls.length) saveBtn.disabled = false;
    };
    img.src = 'img_proxy.php?url=' + encodeURIComponent(url);
  });
}
renderCanvas();

// сохр образа
saveBtn.addEventListener('click', async () => {
  const title = document.getElementById('outfitTitle').value.trim();
  if (!title) {
    alert('Введите название образа');
    return;
  }

  saveBtn.disabled = true;
  saveBtn.textContent = 'Сохранение...';

  const imageData = canvas.toDataURL('image/jpeg', 0.85);

  try {
    const response = await fetch('actions/save_built_outfit.php', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        image_data: imageData,
        title: title,
        description: document.getElementById('outfitDesc').value.trim()
      })
    });
    const result = await response.json();

    if (result.ok) {
      window.location.href = 'wardrobe.php';
    } else {
      alert('Ошибка: ' + result.error);
      saveBtn.disabled = false;
      saveBtn.textContent = 'сохранить образ';
    }
  } catch (err) {
    console.error(err);
    alert('Ошибка соединения');
    saveBtn.disabled = false;
    saveBtn.textContent = 'сохранить образ';
  }
});
</script>

</body>
</html>