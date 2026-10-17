<?php
$secrets = require __DIR__ . '/../secrets.php';

error_log('DB host: ' . $secrets['db_host'] . ', user: ' . $secrets['db_user'] . ', port: ' . $secrets['db_port']);

$dsn = "pgsql:host={$secrets['db_host']};port={$secrets['db_port']};"
     . "dbname={$secrets['db_name']};sslmode=require";

try {
    $pdo = new PDO($dsn, $secrets['db_user'], $secrets['db_pass']);
    $pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $pdo->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC);
} catch (PDOException $e) {
    die("Ошибка подключения к БД: " . $e->getMessage());
}