/*
 
*/

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//чтение строки подключения
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Строка подключения не найдена.");


//преобразование строки подключения
var databaseConnectionString = BuildPostgresConnectionString(rawConnectionString);


//регистрируем DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseConnectionString)
           .EnableSensitiveDataLogging()); //для отладки

//сессии
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();
app.UseSession();

//инициализация бд
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
}

//главная страница
app.MapGet("/", async (HttpContext context) =>
{
    string userName = context.Session.GetString("UserName"); //достаем пользователя из сессии
    bool isLoggedIn = !string.IsNullOrEmpty(userName); //смотрим зареган ли юзер

    /*зависит зареган или нет*/
    string welcomeMessage = isLoggedIn ? $"<h3 style=\"margin:0; color:white;\">Привет, {userName}! Выберете викторину для прохождения</h3>" : "<h3 style=\"margin:0; color:white;\">Вы не авторизованы. Войдите или зарегистрируйтесь, чтобы начать</h3>";

    string buttonsHtml = ""; //все кнопки на странице, тоже зависят от того, вошел пользователь или нет(потом добавим в общ HTML)
    if (isLoggedIn)
    {
        buttonsHtml = @"
                        <form method=""get"" action=""/quizzes/default"">
                            <button type=""submit"" class=""shop-btn"">Дефолт викторины</button>
                        </form>
                        <form method=""get"" action=""/quizzes/custom"">
                            <button type=""submit"" class=""shop-btn"">Викторины от пользователей</button>
                        </form>
                        <form method=""get"" action=""/profile"">
                            <button type=""submit"">Личный кабинет</button>
                        </form>
                        <form method=""post"" action=""/logout"">
                            <button type=""submit"" class=""logout-btn"">Выйти</button>
                        </form>";
    }
    else
    {
        buttonsHtml = @"
                        <form method=""get"" action=""/login"">
                            <button type=""submit"">Войти</button>
                        </form>
                        <form method=""get"" action=""/register"">
                            <button type=""submit"">Регистрация</button>
                        </form>";
    }

    string html = $@"
    <!DOCTYPE html>
    <html>
        <head>
            <title>Пикме викторинкеес</title>
            <meta charset=""UTF-8"">
            <style>
                * {{
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }}

                body {{
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    min-height: 100vh;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    justify-content: center;
                    padding: 20px;
                }}

                .container {{
                    background: white;
                    border-radius: 20px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                    max-width: 500px;
                    width: 100%;
                    text-align: center;
                }}

                h1 {{
                    color: #333;
                    font-size: 2.5em;
                    margin-bottom: 15px;
                    border-bottom: 3px solid #667eea;
                    display: inline-block;
                    padding-bottom: 10px;
                }}

                .welcome-message {{
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                    color: white;
                    padding: 15px;
                    border-radius: 15px;
                    margin: 20px 0;
                }}

                p {{
                    color: #666;
                    font-size: 1.2em;
                    margin: 20px 0;
                    line-height: 1.6;
                }}

                button {{
                    width: 100%;
                    background: #667eea;
                    color: white;
                    border: none;
                    padding: 14px;
                    font-size: 1.1em;
                    border-radius: 10px;
                    cursor: pointer;
                    transition: all 0.3s ease;
                    font-weight: 600;
                    margin-top: 10px;
                }}
            </style>
        </head>
        <body>
            <div class=""container"">
                <h1>Добро пожаловать!</h1>
                
                <div class=""welcome-message"">
                    {welcomeMessage}
                </div>
                
                <div class=""button-group"">
                    {buttonsHtml}
                </div>
            </div>
        </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

//страница регистрации
app.MapGet("/register", () =>
{
    string html = @"
    <!DOCTYPE html>
    <html>
        <head>
            <title>Регистрация</title>
            <meta charset=""UTF-8"">
            <style>
                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }
                body {
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    min-height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 20px;
                }
                .container {
                    background: white;
                    border-radius: 20px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                    max-width: 450px;
                    width: 100%;
                }
                h1 {
                    color: #333;
                    text-align: center;
                    margin-bottom: 30px;
                    border-bottom: 3px solid #667eea;
                    display: inline-block;
                    width: 100%;
                    padding-bottom: 10px;
                }
                .form-group {
                    margin-bottom: 20px;
                }
                label {
                    display: block;
                    margin-bottom: 8px;
                    color: #333;
                    font-weight: 500;
                }
                input {
                    width: 100%;
                    padding: 12px;
                    border: 2px solid #e0e0e0;
                    border-radius: 10px;
                    font-size: 1em;
                    transition: all 0.3s ease;
                }
                button {
                    width: 100%;
                    background: #667eea;
                    color: white;
                    border: none;
                    padding: 14px;
                    font-size: 1.1em;
                    border-radius: 10px;
                    cursor: pointer;
                    transition: all 0.3s ease;
                    font-weight: 600;
                    margin-top: 10px;
                }
            </style>
        </head>
        <body>
            <div class=""container"">
                <h1>Регистрация</h1>
                <form method=""post"" action=""/register"">
                    <div class=""form-group"">
                        <label>Логин:</label>
                        <input type=""text"" name=""login"" required minlength=""3"" />
                    </div>
                    <div class=""form-group"">
                        <label>Пароль:</label>
                        <input type=""password"" name=""password"" required minlength=""4"" />
                    </div>
                    <div class=""form-group"">
                        <label>Подтверждение пароля:</label>
                        <input type=""password"" name=""confirm_password"" required />
                    </div>
                    <button type=""submit"">Зарегистрироваться</button>
                </form>
                <a href=""/"" class=""back-link"">Вернуться на главную</a>
            </div>
        </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

//обработка регистрации
app.MapPost("/register", async (HttpContext context, AppDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();           // |
    string login = form["login"]!;                              // |    достаем из формы введенные данные
    string password = form["password"]!;                        // |
    string confirmPassword = form["confirm_password"]!;         // |

    var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Login == login);   //проверяем существует ли УЖЕ такой пользователь

    string html;

    if (existingUser != null)  //если да, то выводим ошмбко
    {
        html = $@"
        <!DOCTYPE html>
        <html>
            <head>
                <title>Ошибка регистрации. Такой пользователь уже существует</title>
                <meta charset=""UTF-8"">
                <style>
                    * {{
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }}
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                    }}
                    .container {{
                        background: white;
                        border-radius: 20px;
                        padding: 40px;
                        text-align: center;
                        max-width: 500px;
                        width: 100%;
                        margin: 20px;
                    }}
                    h1 {{
                        color: #dc3545;
                        margin-bottom: 20px;
                        border-bottom: 2px solid #dc3545;
                        display: inline-block;
                        padding-bottom: 10px;
                    }}
                    p {{
                        color: #666;
                        margin: 20px 0;
                        font-size: 1.1em;
                    }}
                    .button {{
                        display: inline-block;
                        background: #667eea;
                        color: white;
                        text-decoration: none;
                        padding: 12px 24px;
                        border-radius: 25px;
                        margin: 10px;
                        transition: all 0.3s ease;
                    }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <h1>Ошибка регистрации</h1>
                    <p>Пользователь с логином <strong>'{login}'</strong> уже существует</p>
                    <p>Используйте другой логин или войдите в существующий аккаунт</p>
                    <a href=""/register"" class=""button"">Попробовать снова</a>
                    <a href=""/login"" class=""button"">Войти</a>
                </div>
            </body>
        </html>";
    }
    else if (password != confirmPassword) // если введенные пароль и его подтверждение не совпадают, тоже выыводим ошибко
    {
        html = @"
        <!DOCTYPE html>
        <html>
            <head>
                <title>Ошибка регистрации. Пароли не совпадают</title>
                <meta charset=""UTF-8"">
                <style>
                    * {
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }
                    body {
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                    }
                    .container {
                        background: white;
                        border-radius: 20px;
                        padding: 40px;
                        text-align: center;
                        max-width: 500px;
                        width: 100%;
                        margin: 20px;
                    }
                    h1 {
                        color: #dc3545;
                        margin-bottom: 20px;
                        border-bottom: 2px solid #dc3545;
                        display: inline-block;
                        padding-bottom: 10px;
                    }
                    p {
                        color: #666;
                        margin: 20px 0;
                        font-size: 1.1em;
                    }
                    .button {
                        display: inline-block;
                        background: #667eea;
                        color: white;
                        text-decoration: none;
                        padding: 12px 24px;
                        border-radius: 25px;
                        margin: 10px;
                        transition: all 0.3s ease;
                    }
                </style>
            </head>
            <body>
                <div class=""container"">
                    <h1>Ошибка регистрации</h1>
                    <p>Введенные пароли не совпадают!</p>
                    <p>Пожалуйста, убедитесь, что пароль и подтверждение пароля одинаковые.</p>
                    <a href=""/register"" class=""button"">Попробовать снова</a>
                </div>
            </body>
        </html>";
    }
    else  // если ошибк нет
    {
        string hashed_password = GetSha256Hash(password);  //хешируем пароль
        var user = new User { Login = login, Password = hashed_password }; //создаем нового юзера
        db.Users.Add(user);             // | добавляем 
        await db.SaveChangesAsync();    // | в бд

        html = $@"
        <!DOCTYPE html>
        <html>
            <head>
                <title>Регистрация без ошибок</title>
                <meta charset=""UTF-8"">
                <style>
                    * {{
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }}
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                    }}
                    .container {{
                        background: white;
                        border-radius: 20px;
                        padding: 40px;
                        text-align: center;
                        max-width: 500px;
                        width: 100%;
                        margin: 20px;
                    }}
                    h1 {{
                        color: #28a745;
                        margin-bottom: 20px;
                        border-bottom: 2px solid #28a745;
                        display: inline-block;
                        padding-bottom: 10px;
                    }}
                    p {{
                        color: #666;
                        margin: 20px 0;
                        font-size: 1.1em;
                    }}
                    .button {{
                        display: inline-block;
                        background: #667eea;
                        color: white;
                        text-decoration: none;
                        padding: 12px 24px;
                        border-radius: 25px;
                        margin: 10px;
                        transition: all 0.3s ease;
                    }}
                    .button:hover {{
                        background: #764ba2;
                        transform: translateY(-2px);
                    }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <h1>Регистрация успешна!</h1>
                    <p>Пользователь <strong>{login}</strong> успешно зарегистрирован!</p>
                    <p>Теперь вы можете войти в свой аккаунт.</p>
                    <a href=""/login"" class=""button"">Войти в аккаунт</a>
                    <a href=""/"" class=""button"">На главную</a>
                </div>
            </body>
        </html>";
    }

    return Results.Content(html, "text/html; charset=utf-8");
});
//страница входа
app.MapGet("/login", () =>
{
    string html = @"
    <!DOCTYPE html>
    <html>
        <head>
            <title>Вход</title>
            <meta charset=""UTF-8"">
            <style>
                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }
                body {
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    min-height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 20px;
                }
                .container {
                    background: white;
                    border-radius: 20px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                    max-width: 450px;
                    width: 100%;
                }
                h1 {
                    color: #333;
                    text-align: center;
                    margin-bottom: 30px;
                    border-bottom: 3px solid #667eea;
                    display: inline-block;
                    width: 100%;
                    padding-bottom: 10px;
                }
                .form-group {
                    margin-bottom: 20px;
                }
                label {
                    display: block;
                    margin-bottom: 8px;
                    color: #333;
                    font-weight: 500;
                }
                input {
                    width: 100%;
                    padding: 12px;
                    border: 2px solid #e0e0e0;
                    border-radius: 10px;
                    font-size: 1em;
                    transition: all 0.3s ease;
                }
                button {
                    width: 100%;
                    background: #667eea;
                    color: white;
                    border: none;
                    padding: 14px;
                    font-size: 1.1em;
                    border-radius: 10px;
                    cursor: pointer;
                    transition: all 0.3s ease;
                    font-weight: 600;
                    margin-top: 10px;
                }
                .back-link {
                    display: block;
                    text-align: center;
                    margin-top: 20px;
                    color: #667eea;
                    text-decoration: none;
            </style>
        </head>
        <body>
            <div class=""container"">
                <h1>Вход в аккаунт</h1>
                <form method=""post"" action=""/login"">
                    <div class=""form-group"">
                        <label>Логин:</label>
                        <input type=""text"" name=""login"" required />
                    </div>
                    <div class=""form-group"">
                        <label>Пароль:</label>
                        <input type=""password"" name=""password"" required />
                    </div>
                    <button type=""submit"">Войти</button>
                </form>
                <a href=""/"" class=""back-link"">← Вернуться на главную</a>
                <a href=""/register"" class=""back-link"">Нет аккаунта? Зарегистрироваться</a>
            </div>
        </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

//обработка входа
app.MapPost("/login", async (HttpContext context, AppDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();       // |
    string login = form["login"]!;                          // | читаем из формы
    string password = form["password"]!;                    // | 
    string hashed_password = GetSha256Hash(password);       // хешируем пароль
    var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login && u.Password == hashed_password);   //ищем пользователя

    string html;
    if (user is null) //не найден
    {
        html = @"
        <!DOCTYPE html>
        <html>
            <head>
                <title>Ошибка входа</title>
                <meta charset=""UTF-8"">
                <style>
                    * {
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }
                    body {
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                    }
                    .container {
                        background: white;
                        border-radius: 20px;
                        padding: 40px;
                        box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                        max-width: 500px;
                        width: 100%;
                        text-align: center;
                    }
                    h1 {
                        color: #dc3545;
                        margin-bottom: 20px;
                        border-bottom: 2px solid #dc3545;
                        display: inline-block;
                        padding-bottom: 10px;
                    }
                    .content {
                        margin-top: 30px;
                    }
                    p {
                        color: #666;
                        margin: 15px 0;
                        line-height: 1.6;
                    }
                    .button {
                        display: inline-block;
                        background: #667eea;
                        color: white;
                        text-decoration: none;
                        padding: 12px 24px;
                        border-radius: 25px;
                        margin: 10px;
                        transition: all 0.3s ease;
                    }
                </style>
            </head>
            <body>
                <div class=""container"">
                    <h1>Ошибка входа</h1>
                    <div class=""content"">
                        <p>Неверный логин или пароль!</p>
                        <p>Пожалуйста, проверьте правильность введенных данных.</p>
                        <a href=""/login"" class=""button"">Попробовать снова</a>
                        <a href=""/register"" class=""button"">Зарегистрироваться</a>
                    </div>
                </div>
            </body>
        </html>";
    }
    else //нашли
    {
        context.Session.SetString("UserId", user.Id.ToString());    // |    добавляем
        context.Session.SetString("UserName", user.Login);          // |    в сессию

        html = @"
        <!DOCTYPE html>
        <html>
            <head>
                <title>Вход без ошибко</title>
                <meta charset=""UTF-8"">
                <style>
                    * {
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }
                    body {
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        min-height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                    }
                    .container {
                        background: white;
                        border-radius: 20px;
                        padding: 40px;
                        box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                        max-width: 500px;
                        width: 100%;
                        text-align: center;
                    }
                    h1 {
                        color: #28a745;
                        margin-bottom: 20px;
                        border-bottom: 2px solid #28a745;
                        display: inline-block;
                        padding-bottom: 10px;
                    }
                    .content {
                        margin-top: 30px;
                    }
                    p {
                        color: #666;
                        margin: 15px 0;
                        line-height: 1.6;
                    }
                    .button {
                        display: inline-block;
                        background: #667eea;
                        color: white;
                        text-decoration: none;
                        padding: 12px 24px;
                        border-radius: 25px;
                        margin: 10px;
                        transition: all 0.3s ease;
                    }
                    .button:hover {
                        background: #764ba2;
                        transform: translateY(-2px);
                    }
                </style>
            </head>
            <body>
                <div class=""container"">
                    <h1>Вход выполнен успешно!</h1>
                    <div class=""content"">
                        <p>Здравствуйте, " + user.Login + @"!</p>
                        <a href=""/"" class=""button"">На главную</a>
                    </div>
                </div>
            </body>
        </html>";
    }

    return Results.Content(html, "text/html; charset=utf-8");
});

//выход
app.MapPost("/logout", (HttpContext context) =>
{
    context.Session.Clear(); //прост очищаем сессию
    return Results.Redirect("/");
});

//личный кабинет
app.MapGet("/profile", async (HttpContext context, AppDbContext db) =>
{
    string userId = context.Session.GetString("UserId"); //достаем пользователя из сессии

    if (string.IsNullOrEmpty(userId)) //если в сессии нет пользователя(не был выполнен вход в акк)
    {
        string html = """
        <!DOCTYPE html>
        <html>
            <head><title>Личный кабинет(не выполнен вход в акк)</title><meta charset="UTF-8">
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    min-height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                }
                .container {
                    background: white;
                    border-radius: 20px;
                    padding: 40px;
                    text-align: center;
                }
                h1 { color: #dc3545; margin-bottom: 20px; }
                p { color: #666; margin-bottom: 20px; }
                a {
                    background: #667eea;
                    color: white;
                    text-decoration: none;
                    padding: 12px 24px;
                    border-radius: 25px;
                    display: inline-block;
                }
                a:hover { background: #764ba2; }
            </style>
            </head>
            <body>
                <div class="container">
                    <h1>Не выполнен вход в аккаунт</h1>
                    <p>Войдите в аккаунт</p>
                    <a href="/login">Войти</a>
                </div>
            </body>
        </html>
        """;
        return Results.Content(html, "text/html; charset=utf-8");
    }
    //пользователь есть
    var user = await db.Users.FindAsync(int.Parse(userId)); //достаем его из бд
    string userName = context.Session.GetString("UserName")!; //достаем логин из ьд

    string profileHtml = $$"""
    <!DOCTYPE html>
    <html>
        <head>
            <title>Личный кабинет</title>
            <meta charset="UTF-8">
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    min-height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 20px;
                }
                .container {
                    background: white;
                    border-radius: 20px;
                    padding: 40px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                    max-width: 500px;
                    width: 100%;
                    text-align: center;
                }
                h1 {
                    color: #333;
                    margin-bottom: 30px;
                    border-bottom: 3px solid #667eea;
                    display: inline-block;
                    padding-bottom: 10px;
                }
                .info-card {
                    background: #f8f9ff;
                    border-radius: 15px;
                    padding: 25px;
                    margin: 20px 0;
                    border: 2px solid #667eea;
                }
                .info-item {
                    margin: 15px 0;
                    padding: 10px;
                    border-bottom: 1px solid #e0e0e0;
                }
                .info-label {
                    font-weight: bold;
                    color: #667eea;
                }
                .info-value {
                    color: #333;
                    margin-top: 5px;
                    font-size: 1.2em;
                }
                .button-group {
                    display: flex;
                    gap: 15px;
                    justify-content: center;
                    margin-top: 20px;
                }
                button, a.button {
                    background: #667eea;
                    color: white;
                    border: none;
                    padding: 12px 24px;
                    font-size: 1em;
                    border-radius: 50px;
                    cursor: pointer;
                    text-decoration: none;
                    display: inline-block;
                    transition: all 0.3s ease;
                }
                button:hover, a.button:hover {
                    background: #764ba2;
                    transform: translateY(-2px);
                }
                .logout-btn {
                    background: #dc3545;
                }
                .logout-btn:hover {
                    background: #c82333;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <h1>Личный кабинет</h1>
                
                <div class="info-card">
                    <div class="info-item">
                        <div class="info-label">Логин:</div>
                        <div class="info-value">{{userName}}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">ID пользователя:</div>
                        <div class="info-value">{{user!.Id}}</div>
                    </div>
                </div>
                <div>
                    <div class="info-item">
                        <div class="info-label">Логин:</div>
                        <div class="info-value">{{user.QuizzesPassed}}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">Логин:</div>
                        <div class="info-value">{{user.QuizzesCreated}}</div>
                    </div>
                    <div class="info-item">
                        <div class="info-label">Логин:</div>
                        <div class="info-value">{{user.BestScore}}</div>
                    </div>
                </div>
                <div class="button-group">
                    <a href="/shop" class="button">Перейти в магазин</a>
                    <form method="post" action="/logout" style="display: inline;">
                        <button type="submit" class="logout-btn">Выйти</button>
                    </form>
                </div>
                <div style="margin-top: 15px;">
                    <a href="/" class="button" style="background: #6c757d;">На главную</a>
                </div>
            </div>
        </body>
    </html>
    """;

    return Results.Content(profileHtml, "text/html; charset=utf-8");
});

//деф викторинф
app.MapGet("/quizzes/default", async (AppDbContext db, HttpContext context) =>
{
    string userId = context.Session.GetString("UserId");
    bool isLoggedIn = !string.IsNullOrEmpty(userId);

    // админ — это пользователь с Id = 1(создатель как бы деф викторин)
    int adminId = 1;

    //получаем только админские викторины
    var quizzes = await db.Quizzes
        .Include(q => q.Creator)
        .Where(q => q.CreatorId == adminId)
        .OrderByDescending(q => q.TimesPlayed)
        .ToListAsync();

    // формируем карточки через строку, которую потом добавим в общ HTML
    string quizzesHtml = "";
    foreach (var quiz in quizzes)
    {
        quizzesHtml += $@"
        <a href='/quiz/{quiz.Id}' class='quiz-card-link'>
            <div class='quiz-card'>
                <h3>{quiz.Title}</h3>
                <p>{quiz.Description ?? "Описание отсутствует"}</p>
                <div class='quiz-stats'>
                    <span>{quiz.Questions.Count} вопросов</span>
                    <span>{quiz.TotalPoints} баллов</span>
                    <span>{quiz.TimesPlayed} игр</span>
                    <span>Средний балл: {quiz.AverageScore}%</span>
                </div>
                <div class='quiz-creator'>Автор: Админко</div>
            </div>
        </a>";
    }


    string html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Деф викторинки</title>
        <meta charset='UTF-8'>
        <style>
    * {{
        margin: 0;
        padding: 0;
        box-sizing: border-box;
    }}
    
    body {{
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        background: white;
        color: black;
        padding: 40px;
    }}
    
    .container {{
        max-width: 1200px;
        margin: 0 auto;
    }}
    
    /* Заголовки */
    h1 {{
        font-size: 2.5em;
        margin-bottom: 20px;
        border-bottom: 3px solid black;
        display: inline-block;
    }}
    
    h2 {{
        font-size: 1.8em;
        margin: 30px 0 20px 0;
        border-bottom: 1px solid #ccc;
        padding-bottom: 5px;
    }}
    
    /* Навигация */
    .nav-buttons {{
        text-align: center;
        margin-bottom: 40px;
        border-bottom: 1px solid #ccc;
        padding-bottom: 20px;
    }}
    
    .nav-btn {{
        background: black;
        color: white;
        text-decoration: none;
        padding: 8px 16px;
        margin: 0 5px;
        display: inline-block;
        font-family: monospace;
        border: 1px solid black;
    }}
    
    .nav-btn:hover {{
        background: white;
        color: black;
    }}
    
    /* Кнопки */
    button, .button {{
        background: black;
        color: white;
        border: none;
        padding: 10px 20px;
        cursor: pointer;
        font-family: monospace;
        font-size: 1em;
        display: inline-block;
        text-decoration: none;
    }}
    
    button:hover, .button:hover {{
        background: #333;
    }}
    
    /* Формы */
    input, textarea, select {{
        width: 100%;
        padding: 8px;
        margin: 5px 0 15px 0;
        border: 1px solid black;
        font-family: monospace;
        background: white;
    }}
    
    label {{
        font-weight: bold;
    }}
    
    /* Карточки викторин */
    .quiz-grid {{
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
        gap: 20px;
        margin: 20px 0;
    }}
    
    .quiz-card {{
        border: 1px solid black;
        padding: 20px;
        background: white;
    }}
    
    .quiz-card:hover {{
        background: #f5f5f5;
    }}
    
    .quiz-card-link {{
        text-decoration: none;
        color: black;
    }}
    
    .quiz-badge {{
        display: inline-block;
        padding: 4px 8px;
        margin-bottom: 10px;
        font-size: 0.8em;
        font-weight: bold;
    }}
    
    .quiz-badge.official {{
        background: black;
        color: white;
    }}
    
    .quiz-badge.user {{
        background: #ccc;
        color: black;
    }}
    
    .quiz-stats {{
        margin: 10px 0;
        font-size: 0.9em;
        color: #555;
    }}
    
    .quiz-stats span {{
        margin-right: 15px;
    }}
    
    .quiz-creator {{
        margin-top: 10px;
        padding-top: 10px;
        border-top: 1px solid #ccc;
        font-size: 0.85em;
        color: #777;
    }}
    
    /* Блоки вопросов */
    .question-block {{
        border: 1px solid black;
        padding: 20px;
        margin-bottom: 20px;
        background: white;
    }}
    
    .question-header {{
        display: flex;
        justify-content: space-between;
        margin-bottom: 15px;
        padding-bottom: 5px;
        border-bottom: 1px solid black;
    }}
    
    .question-num {{
        font-weight: bold;
    }}
    
    .question-points {{
        color: black;
        font-weight: bold;
    }}
    
    .question-text {{
        margin-bottom: 15px;
    }}
    
    .answers {{
        display: flex;
        flex-direction: column;
        gap: 10px;
    }}
    
    .answer-option {{
        display: flex;
        align-items: center;
        gap: 10px;
    }}
    
    input[type=""radio""] {{
        width: auto;
        margin: 0;
    }}
    
    /* Результат */
    .result-box {{
        border: 1px solid black;
        padding: 40px;
        text-align: center;
        margin: 40px 0;
    }}
    
    .score {{
        font-size: 48px;
        font-weight: bold;
        margin: 20px 0;
    }}
    
    /* Пустое состояние */
    .empty-state {{
        border: 1px solid #ccc;
        padding: 60px;
        text-align: center;
        color: #777;
    }}
    
    /* Форма создания викторины */
    .form-group {{
        margin-bottom: 20px;
    }}
    
    /* Профиль */
    .info-card {{
        border: 1px solid black;
        padding: 20px;
        margin: 20px 0;
    }}
    
    .info-item {{
        margin: 10px 0;
        padding: 5px 0;
        border-bottom: 1px solid #eee;
    }}
    
    .info-label {{
        font-weight: bold;
        width: 150px;
        display: inline-block;
    }}
    
    .button-group {{
        display: flex;
        gap: 10px;
        margin-top: 20px;
    }}
    
    @media (max-width: 600px) {{
        body {{ padding: 20px; }}
        .quiz-grid {{ grid-template-columns: 1fr; }}
        .button-group {{ flex-direction: column; }}
        .info-label {{ width: 100%; display: block; margin-bottom: 5px; }}
    }}
</style>
    </head>
    <body>
        <div class='container'>
            <div class='nav-buttons'>
                <a href='/' class='nav-btn'>Главная</a>
                <a href='/quizzes/default' class='nav-btn active'>Деф</a>
                <a href='/quizzes/custom' class='nav-btn'>Пользовательские</a>
                <a href='/profile' class='nav-btn'>Профиль</a>
            </div>
            
            
            <div class='quiz-grid'>
                {quizzesHtml}
            </div>
        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});
//пользовательские викторины
app.MapGet("/quizzes/custom", async (AppDbContext db, HttpContext context) =>
{
    string userId = context.Session.GetString("UserId");
    bool isLoggedIn = !string.IsNullOrEmpty(userId);
    int currentUserId = isLoggedIn ? int.Parse(userId) : 0;

    int adminId = 1;

    //получаем викторинки
    var quizzes = await db.Quizzes
        .Include(q => q.Creator)
        .Where(q => q.CreatorId != adminId)
        .ToListAsync();

    //формируем их карточки
    string quizzesHtml = "";
    foreach (var quiz in quizzes)
    {
        string creatorName = quiz.Creator?.Login ?? "Неизвестный";
        bool isCurrentUserCreator = isLoggedIn && quiz.CreatorId == currentUserId;

        quizzesHtml += $@"
        <a href='/quiz/{quiz.Id}' class='quiz-card-link'>
            <div class='quiz-card'>
                <div class='quiz-badge'>Пользовательская</div>
                <h3>{quiz.Title}</h3>
                <p>{quiz.Description ?? "Описание отсутствует"}</p>
                <div class='quiz-stats'>
                    <span>{quiz.Questions.Count} вопросов</span>
                    <span>{quiz.TotalPoints} баллов</span>
                    <span>{quiz.TimesPlayed} игр</span>
                </div>
                <div class='quiz-creator'>
                    Автор: {creatorName}
                    {(isCurrentUserCreator ? "<span style='color: #28a745; margin-left: 10px;'>Ваша викторина</span>" : "")}
                </div>
            </div>
        </a>";
    }

    //если нет пользовательских викторин
    if (string.IsNullOrEmpty(quizzesHtml))
    {
        quizzesHtml = $@"
        <div class='empty-state'>
            <p>Пока нет пользовательских викторин</p>
        </div>";
    }

    string html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Пользовательские викторины</title>
        <meta charset='UTF-8'>
        <style>
    * {{
        margin: 0;
        padding: 0;
        box-sizing: border-box;
    }}
    
    body {{
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        background: white;
        color: black;
        padding: 40px;
    }}
    
    .container {{
        max-width: 1200px;
        margin: 0 auto;
    }}
    
    /* Заголовки */
    h1 {{
        font-size: 2.5em;
        margin-bottom: 20px;
        border-bottom: 3px solid black;
        display: inline-block;
    }}
    
    h2 {{
        font-size: 1.8em;
        margin: 30px 0 20px 0;
        border-bottom: 1px solid #ccc;
        padding-bottom: 5px;
    }}
    
    /* Навигация */
    .nav-buttons {{
        text-align: center;
        margin-bottom: 40px;
        border-bottom: 1px solid #ccc;
        padding-bottom: 20px;
    }}
    
    .nav-btn {{
        background: black;
        color: white;
        text-decoration: none;
        padding: 8px 16px;
        margin: 0 5px;
        display: inline-block;
        font-family: monospace;
        border: 1px solid black;
    }}
    
    .nav-btn:hover {{
        background: white;
        color: black;
    }}
    
    /* Кнопки */
    button, .button {{
        background: black;
        color: white;
        border: none;
        padding: 10px 20px;
        cursor: pointer;
        font-family: monospace;
        font-size: 1em;
        display: inline-block;
        text-decoration: none;
    }}
    
    button:hover, .button:hover {{
        background: #333;
    }}
    
    /* Формы */
    input, textarea, select {{
        width: 100%;
        padding: 8px;
        margin: 5px 0 15px 0;
        border: 1px solid black;
        font-family: monospace;
        background: white;
    }}
    
    label {{
        font-weight: bold;
    }}
    
    /* Карточки викторин */
    .quiz-grid {{
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
        gap: 20px;
        margin: 20px 0;
    }}
    
    .quiz-card {{
        border: 1px solid black;
        padding: 20px;
        background: white;
    }}
    
    .quiz-card:hover {{
        background: #f5f5f5;
    }}
    
    .quiz-card-link {{
        text-decoration: none;
        color: black;
    }}
    
    .quiz-badge {{
        display: inline-block;
        padding: 4px 8px;
        margin-bottom: 10px;
        font-size: 0.8em;
        font-weight: bold;
    }}
    
    .quiz-badge.official {{
        background: black;
        color: white;
    }}
    
    .quiz-badge.user {{
        background: #ccc;
        color: black;
    }}
    
    .quiz-stats {{
        margin: 10px 0;
        font-size: 0.9em;
        color: #555;
    }}
    
    .quiz-stats span {{
        margin-right: 15px;
    }}
    
    .quiz-creator {{
        margin-top: 10px;
        padding-top: 10px;
        border-top: 1px solid #ccc;
        font-size: 0.85em;
        color: #777;
    }}
    
    /* Блоки вопросов */
    .question-block {{
        border: 1px solid black;
        padding: 20px;
        margin-bottom: 20px;
        background: white;
    }}
    
    .question-header {{
        display: flex;
        justify-content: space-between;
        margin-bottom: 15px;
        padding-bottom: 5px;
        border-bottom: 1px solid black;
    }}
    
    .question-num {{
        font-weight: bold;
    }}
    
    .question-points {{
        color: black;
        font-weight: bold;
    }}
    
    .question-text {{
        margin-bottom: 15px;
    }}
    
    .answers {{
        display: flex;
        flex-direction: column;
        gap: 10px;
    }}
    
    .answer-option {{
        display: flex;
        align-items: center;
        gap: 10px;
    }}
    
    input[type=""radio""] {{
        width: auto;
        margin: 0;
    }}
    
    /* Результат */
    .result-box {{
        border: 1px solid black;
        padding: 40px;
        text-align: center;
        margin: 40px 0;
    }}
    
    .score {{
        font-size: 48px;
        font-weight: bold;
        margin: 20px 0;
    }}
    
    /* Пустое состояние */
    .empty-state {{
        border: 1px solid #ccc;
        padding: 60px;
        text-align: center;
        color: #777;
    }}
    
    /* Форма создания викторины */
    .form-group {{
        margin-bottom: 20px;
    }}
    
    /* Профиль */
    .info-card {{
        border: 1px solid black;
        padding: 20px;
        margin: 20px 0;
    }}
    
    .info-item {{
        margin: 10px 0;
        padding: 5px 0;
        border-bottom: 1px solid #eee;
    }}
    
    .info-label {{
        font-weight: bold;
        width: 150px;
        display: inline-block;
    }}
    
    .button-group {{
        display: flex;
        gap: 10px;
        margin-top: 20px;
    }}
    
    @media (max-width: 600px) {{
        body {{ padding: 20px; }}
        .quiz-grid {{ grid-template-columns: 1fr; }}
        .button-group {{ flex-direction: column; }}
        .info-label {{ width: 100%; display: block; margin-bottom: 5px; }}
    }}
</style>
    </head>
    <body>
        <div class='container'>
            <div class='nav-buttons'>
                <a href='/' class='nav-btn'>Главная</a>
                <a href='/quizzes/default' class='nav-btn'>Официальные</a>
                <a href='/quizzes/custom' class='nav-btn active'>Пользовательские</a>
                <a href='/profile' class='nav-btn'>Профиль</a>
                {(isLoggedIn ? "<a href='/create-quiz' class='nav-btn'>Создать</a>" : "")}
            </div>
            
            <div class='header'>
                <h1>Пользовательские викторины</h1>
                <div class='subtitle'>Викторины, созданные участниками сайта</div>
            </div>
            
            <div class='quiz-grid'>
                {quizzesHtml}
            </div>
        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});// Пользовательские викторины (от всех пользователей, кроме админа)
app.Run();
static string BuildPostgresConnectionString(string rawConnectionString) //жоска украденный код по преобразованию строки
{
    // Если строка уже записана в обычном формате Host=...;Port=...;Database=...,
    // просто возвращаем ее без изменений.
    if (!rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
        !rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        // Возвращаем исходную строку подключения как есть.
        return rawConnectionString;
    }

    // Преобразуем текстовую URI-строку в объект Uri, чтобы удобно разобрать ее на части.
    var databaseUri = new Uri(rawConnectionString);

    // Разделяем блок user:password на логин и пароль.
    var userInfoParts = databaseUri.UserInfo.Split(':', 2, StringSplitOptions.None);

    // Проверяем, что в URI действительно были и логин, и пароль.
    if (userInfoParts.Length != 2)
    {
        // Если формат неожиданно неверный, сообщаем об ошибке понятным текстом.
        throw new InvalidOperationException("Не удалось разобрать логин и пароль из URI-строки подключения PostgreSQL.");
    }

    // Создаем построитель строки подключения Npgsql.
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
        // Записываем адрес сервера базы данных.
        Host = databaseUri.Host,

        // Если порт в URI не указан, используем стандартный порт PostgreSQL 5432.
        Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,

        // Берем имя базы из пути URI и убираем ведущий символ "/".
        Database = databaseUri.AbsolutePath.Trim('/'),

        // Записываем имя пользователя.
        Username = Uri.UnescapeDataString(userInfoParts[0]),

        // Записываем пароль пользователя.
        Password = Uri.UnescapeDataString(userInfoParts[1])
    };

    // Разбиваем query-параметры URI, например sslmode=require&channel_binding=require.
    foreach (var queryPart in databaseUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        // Разделяем параметр на имя и значение по первому символу "=".
        var queryPartPieces = queryPart.Split('=', 2, StringSplitOptions.None);

        // Декодируем имя параметра, если в нем были специальные символы.
        var key = Uri.UnescapeDataString(queryPartPieces[0]);

        // Декодируем значение параметра или используем пустую строку, если значения нет.
        var value = queryPartPieces.Length > 1
            ? Uri.UnescapeDataString(queryPartPieces[1])
            : string.Empty;

        // Для параметра sslmode используем точное имя ключа, которое ожидает Npgsql.
        if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
        {
            // Сохраняем режим SSL в стандартный ключ строки подключения Npgsql.
            connectionStringBuilder["SSL Mode"] = value;

            // Переходим к следующему query-параметру.
            continue;
        }

        // Для параметра channel_binding тоже используем точное имя ключа Npgsql.
        if (key.Equals("channel_binding", StringComparison.OrdinalIgnoreCase))
        {
            // Сохраняем настройку Channel Binding в строку подключения.
            connectionStringBuilder["Channel Binding"] = value;

            // Переходим к следующему query-параметру.
            continue;
        }

        // Все остальные query-параметры тоже добавляем в строку подключения без потери.
        connectionStringBuilder[key] = value;
    }

    // Возвращаем готовую строку подключения в стандартном формате Npgsql.
    return connectionStringBuilder.ConnectionString;
}
static string GetSha256Hash(string input)//хеширование паролей
{
    using var sha256 = SHA256.Create();
    byte[] bytes = Encoding.UTF8.GetBytes(input);
    byte[] hashBytes = sha256.ComputeHash(bytes);

    var sb = new StringBuilder();
    foreach (byte b in hashBytes)
        sb.Append(b.ToString("x2"));

    return sb.ToString();
}
public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
    public int TotalPoints { get; set; } = 0;
    public int QuizzesPassed { get; set; } = 0;
    public int QuizzesCreated { get; set; } = 0;
    public int BestScore { get; set; } = 0;

    public ICollection<Quiz> CreatedQuizzes { get; set; } = new List<Quiz>();
    public ICollection<Result> Results { get; set; } = new List<Result>();
}

public class Quiz
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int CreatorId { get; set; }
    public int Difficulty { get; set; } = 1;
    public int TotalPoints { get; set; } = 0;
    public int QuestionsCount { get; set; } = 0;
    public int TimesPlayed { get; set; } = 0;
    public decimal AverageScore { get; set; } = 0;

    public User? Creator { get; set; }
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<Result> Results { get; set; } = new List<Result>();
}

public class Question
{
    public int Id { get; set; }
    public int QuizId { get; set; }
    public string Text { get; set; } = "";
    public int Points { get; set; } = 1;
    public int OrderNum { get; set; } = 0;
    public string Type { get; set; } = "single";

    public Quiz? Quiz { get; set; }
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
}

public class Answer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; } = false;
    public int OrderNum { get; set; } = 0;

    public Question? Question { get; set; }
}

public class Result
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuizId { get; set; }
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public decimal? Percentage { get; set; }

    public User? User { get; set; }
    public Quiz? Quiz { get; set; }
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
}

public class UserAnswer
{
    public int Id { get; set; }
    public int ResultId { get; set; }
    public int QuestionId { get; set; }
    public int? AnswerId { get; set; }
    public bool IsCorrect { get; set; } = false;

    public Result? Result { get; set; }
    public Question? Question { get; set; }
    public Answer? Answer { get; set; }
}
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<Result> Results { get; set; }
    public DbSet<UserAnswer> UserAnswers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //уникальный логин
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Login)
            .IsUnique();

        //удаление вопросов при удалении викторины
        modelBuilder.Entity<Question>()
            .HasOne(q => q.Quiz)
            .WithMany(q => q.Questions)
            .HasForeignKey(q => q.QuizId)
            .OnDelete(DeleteBehavior.Cascade);

        //удаление ответов при удалении вопроса
        modelBuilder.Entity<Answer>()
            .HasOne(a => a.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}