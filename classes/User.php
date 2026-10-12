<?php
require_once __DIR__ . '/Model.php';

class User extends Model
{

    public function existsByUsernameOrEmail(string $username, string $email): bool
    {
        $stmt = $this->pdo->prepare(
            "SELECT id FROM users WHERE username = ? OR email = ?"
        );
        $stmt->execute([$username, $email]);
        return (bool) $stmt->fetch();
    }

    public function register(string $username, string $email, string $password): int
    {
        $hash = password_hash($password, PASSWORD_DEFAULT);
        $stmt = $this->pdo->prepare(
            "INSERT INTO users (username, email, password_hash)
             VALUES (?, ?, ?) RETURNING id"
        );
        $stmt->execute([$username, $email, $hash]);
        $user = $stmt->fetch();
        return (int) $user['id'];
    }

    public function findByLoginOrEmail(string $login): ?array
    {
        $stmt = $this->pdo->prepare(
            "SELECT id, username, password_hash FROM users
             WHERE username = ? OR email = ?"
        );
        $stmt->execute([$login, $login]);
        $user = $stmt->fetch();
        return $user ?: null;
    }

    public function verifyPassword(string $password, string $hash): bool
    {
        return password_verify($password, $hash);
    }
}