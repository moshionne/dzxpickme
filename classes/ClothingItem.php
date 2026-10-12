<?php
require_once __DIR__ . '/Model.php';

class ClothingItem extends Model
{
    public function getAllByUser(int $userId): array
    {
        $stmt = $this->pdo->prepare(
            "SELECT id, user_id, name, image_url, source_url
             FROM clothing_items
             WHERE user_id = ?
             ORDER BY id DESC"
        );
        $stmt->execute([$userId]);
        return $stmt->fetchAll();
    }

    public function save(
        int $userId,
        string $name,
        string $imageUrl,
        ?string $sourceUrl = null
    ): bool {
        // проверка на дубликат
        $stmt = $this->pdo->prepare(
            "SELECT id FROM clothing_items WHERE user_id = ? AND image_url = ?"
        );
        $stmt->execute([$userId, $imageUrl]);
        if ($stmt->fetch()) {
            return false; // уже сохранено
        }

        $stmt = $this->pdo->prepare(
            "INSERT INTO clothing_items (user_id, name, image_url, source_url)
             VALUES (?, ?, ?, ?)"
        );
        $stmt->execute([$userId, $name, $imageUrl, $sourceUrl]);
        return true;
    }

    public function delete(int $userId, string $imageUrl): bool
    {
        $stmt = $this->pdo->prepare(
            "DELETE FROM clothing_items WHERE user_id = ? AND image_url = ?"
        );
        $stmt->execute([$userId, $imageUrl]);
        return $stmt->rowCount() > 0;
    }
}