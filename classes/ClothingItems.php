<?php

class ClothingItem
{
    private PDO $pdo;

    public function __construct(PDO $pdo)
    {
        $this->pdo = $pdo;
    }

    /**
     * все вещи юзера
     */
    public function getAllByUser(int $userId): array
    {
        $stmt = $this->pdo->prepare(
            "SELECT * FROM clothing_items WHERE user_id = ? ORDER BY id DESC"
        );
        $stmt->execute([$userId]);
        return $stmt->fetchAll();
    }

    /**
     * добавенияе вещи
     */
    public function create(
        int $userId,
        string $name,
        string $imagePath,
        string $category,
        ?string $color = null
    ): void {
        $stmt = $this->pdo->prepare(
            "INSERT INTO clothing_items (user_id, name, image_path, category, color)
             VALUES (?, ?, ?, ?, ?)"
        );
        $stmt->execute([$userId, $name, $imagePath, $category, $color]);
    }
}