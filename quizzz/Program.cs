using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

var builder = WebApplication.CreateBuilder(args);


// аутентификация с помощью куки
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

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

app.UseStaticFiles();
app.UseAuthentication();   // добавление middleware аутентификации 
app.UseAuthorization();   // добавление middleware авторизации 
//инициализация бд
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
}

//главная страница
app.MapGet("/", async (HttpContext context) =>
{
    bool isLoggedIn = context.User.Identity?.IsAuthenticated == true;
    string userName = context.User.Identity?.Name ?? "";

    string welcomeMessage = isLoggedIn ? $"Hello, {userName}!" : "Welcome!";

    string buttonsHtml = "";
    if (isLoggedIn)
    {
        buttonsHtml = @"
            <form method=""get"" action=""/quizzes"">
                <button type=""submit"" class=""btn shop-btn"">Викторинки</button>
            </form>
            <form method=""get"" action=""/leaderboard"">
                <button type=""submit"" class=""btn shop-btn"">Список лидеров</button>
            </form>
            <form method=""get"" action=""/profile"">
                <button type=""submit"" class=""btn shop-btn"">Личный кабинет</button>
            </form>
            <form method=""post"" action=""/logout"">
                <button type=""submit"" class=""btn logout-btn"">Выйти</button>
            </form>";
    }
    else
    {
        buttonsHtml = @"
            <form method=""get"" action=""/register"">
                <button type=""submit"" class=""btn btn_signin"">Sign in →</button>
            </form>
            <form method=""get"" action=""/login"">
                <button type=""submit"" class=""btn btn_login"">Log in →</button>
            </form>";
    }

    string html = $@"
    <!DOCTYPE html>
    <html>
        <head>
            <title>Starter Page</title>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
        </head>
        <body>
        <div class=""starter-container"">
            <div class=""split-layout"">
                <div class=""left-side"">
                    
                </div>

                <div class=""right-side"">
                    <div class=""welcome"">
                        <div class=""welcome_title""> {welcomeMessage} </div>
                        <div class=""welcome_sub"">🏵</div>
                    </div>
                    <div class=""buttons-stack"">
                        {buttonsHtml}
                    </div>
                </div>
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
            <title>Registration</title>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
        </head>
        <body>
            <div class=""container"">
                <h1>⋆.࿔🌸 Registration *:･</h1>
                <form method=""post"" action=""/register"">

                    <div class=""form-group"">
                        <input type=""text"" name=""login"" id=""login"" required minlength=""3"" placeholder="" "" />
                        <label for=""login"">login:</label>
                        <div class=""input-note""></div>
                    </div>

                    <div class=""form-group"">
                        <input type=""password"" name=""password"" id=""password"" required minlength=""4"" placeholder="" "" />
                        <label for=""password"">password:</label>
                        <div class=""input-note""></div>
                    </div>

                    <div class=""form-group"">
                        <input type=""password"" name=""confirm_password"" id=""confirm_password"" required placeholder="" "" />
                        <label for=""confirm_password"">password confirmation:</label>
                        <div class=""input-note""></div>
                    </div>

                    <button type=""submit"">sign in</button>

                </form>
                <a href=""/"" class=""back-link"">return</a>
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
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>
            </head>
            <body>
                <div class=""container"">
                    <h1>❀  Ошибка регистрации  ❀</h1>
                    <p>Пользователь с логином <strong>'{login}'</strong> уже существует</p>
                    <p>Используйте другой логин или войдите в существующий аккаунт</p>
                    <a href=""/register"" class=""button"">попробовать снова</a>
                    <a href=""/login"" class=""button"">войти</a>
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
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <link rel='stylesheet' href='/css/style.css'>
            </head>
            <body>
                <div class=""container"">
                    <h1>❀  Ошибка регистрации  ❀</h1>
                    <p>Введенные пароли <strong>не совпадают</strong>!</p>
                    <p>Пожалуйста, убедитесь, что пароль и подтверждение пароля одинаковые.</p>
                    <a href=""/register"" class=""button"">попробовать снова</a>
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
                <title>Регистрация успешна</title>
                <meta charset=""UTF-8"">
                 <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                 <link rel='stylesheet' href='/css/style.css'>
            </head>
            <body>
                <div class=""container"">
                    <h1>✿  Регистрация успешна!  ✿</h1>
                    <p>Пользователь <strong>{login}</strong> успешно зарегистрирован!</p>
                    <p>Теперь вы можете войти в свой аккаунт.</p>
                    <a href=""/login"" class=""button"">войти в аккаунт</a>
                    <a href=""/"" class=""button"">на главную</a>
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
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
        </head>
        <body>
            <div class=""container"">
                <h1>⋆.࿔🌸 Log in *:･</h1>
                <form method=""post"" action=""/login"">
                    <div class=""form-group"">
                        <input type=""text"" name=""login"" id=""login"" required placeholder="" "" />
                        <label for=""login"">login:</label>
                    </div>
                    <div class=""form-group"">
                        <input type=""password"" name=""password"" id=""password"" required placeholder="" "" />
                        <label for=""password"">password:</label>
                    </div>
                    <button type=""submit"">Войти</button>
                </form>
                <div class=""links-container"">
                    <a href=""/"" class=""back-link"">return</a>
                    <a href=""/register"" class=""back-link"">no acc? let's create</a>
                </div>
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
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
        </head>
        <body>
            <div class=""container"">
                <div class=""error-icon"">⚠️</div>
                <h1>Ошибка входа</h1>
                <div class=""content"">
                    <div class=""error-message"">
                        <p style=""margin: 0;"">❌ Неверный логин или пароль!</p>
                    </div>
                    <p>Пожалуйста, проверьте правильность введенных данных.</p>
                    <div class=""buttons-container"">
                        <a href=""/login"" class=""button"">Попробовать снова</a>
                        <a href=""/register"" class=""button"">Зарегистрироваться</a>
                    </div>
                </div>
            </div>
        </body>
    </html>";
    }
    else //нашли
    {

        html = @"
        <!DOCTYPE html>
        <html>
            <head>
                <title>Вход выполнен успешно</title>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <link rel='stylesheet' href='/css/style.css'>
            </head>
            <body>
                <div class=""container"">
                    <div class=""success-icon"">✅</div>
                    <h1>Вход выполнен успешно!</h1>
                    <div class=""content"">
                        <div class=""welcome-message"">
                            <p style=""margin: 0;"">🌸 Здравствуйте, " + user.Login + @"! 🌸</p>
                        </div>
                        <a href=""/"" class=""button"">На главную</a>
                    </div>
                </div>
            </body>
        </html>";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Login),
            new Claim("UserId", user.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

        return Results.Redirect("/");
    }   
    return Results.Content(html, "text/html; charset=utf-8");
});

//выход
app.MapPost("/logout", (HttpContext context) =>
{
    context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

//личный кабинет
app.MapGet("/profile", async (HttpContext context, AppDbContext db) =>
{
    string userId = context.User.FindFirstValue("UserId");
    if (string.IsNullOrEmpty(userId)) //если в сессии нет пользователя(не был выполнен вход в акк)
    {
        string html = """
        <!DOCTYPE html>
        <html>
            <head><title>Личный кабинет(не выполнен вход в акк)</title>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
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
    string userName = context.User.Identity?.Name;

    string profileHtml = $$"""
    <!DOCTYPE html>
    <html>
        <head>
            <title>Личный кабинет</title>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
        </head>
        <body>
            <div class="container profile-container">
                <h1>🌸 Личный кабинет 🌸</h1>

                <div class="profile-wrapper">
                    <!-- Левая часть с аватаром -->
                    <div class="profile-left">
                        <div class="avatar">
                            🌸
                        </div>
                        <div class="username">{{userName}}</div>
                        <div class="user-id">
                            🆔 ID: {{user!.Id}}
                        </div>
                    </div>

                    <!-- Правая часть со статистикой -->
                    <div class="profile-right">
                        <div class="stats-card">
                            <h3>Моя статистика</h3>
                            <div class="stats-grid">
                                <div class="stat-item">
                                    <div class="stat-label">📊 Пройденные викторины</div>
                                    <div class="stat-value">{{user.QuizzesPassed}} <span class="stat-unit">шт.</span></div>
                                </div>
                                <div class="stat-item">
                                    <div class="stat-label">✏️ Созданные викторины</div>
                                    <div class="stat-value">{{user.QuizzesCreated}} <span class="stat-unit">шт.</span></div>
                                </div>
                                <div class="stat-item stat-item-large">
                                    <div class="stat-label stat-label-large">🏆 Лучший счет</div>
                                    <div class="stat-value stat-value-large">{{user.BestScore}} <span class="stat-unit">очков</span></div>
                                </div>
                            </div>
                            <div class="stats-footer">
                                ✨ Продолжай в том же духе! ✨
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Кнопки внизу -->
                <div class="button-group">
                    <a href="/leaderboard" class="button">🏅 Список лидеров</a>
                    <form method="post" action="/logout" style="display: inline;">
                        <button type="submit" class="logout-btn">🚪 Выйти</button>
                    </form>
                    <a href="/" class="button home-btn">🏠 На главную</a>
                </div>
            </div>
        </body>
    </html>
    """;

    return Results.Content(profileHtml, "text/html; charset=utf-8");
});

//деф викторинф
app.MapGet("/quizzes", async (AppDbContext db, HttpContext context) =>
{
    string userId = context.User.FindFirstValue("UserId");

    // админ — это пользователь с Id = 1(создатель как бы деф викторин)
    int adminId = 1;

    //получаем только админские викторины
    var defquizzes = await db.Quizzes
        .Include(q => q.Creator)
        .Where(q => q.CreatorId == adminId)
        .OrderByDescending(q => q.TimesPlayed)
        .ToListAsync();

    // формируем карточки через строку, которую потом добавим в общ HTML
    string defquizzesHtml = "";
    foreach (var quiz in defquizzes)
    {
        defquizzesHtml += $@"
        <a href='/quiz/{quiz.Id}' class='quiz-card-link'>
            <div class='quiz-card'>
                <div class='quiz-badge official'>Официальная</div>
                <h3>{quiz.Title}</h3>
                <p>{quiz.Description ?? "Описание отсутствует"}</p>
                <div class='quiz-stats'>
                    <span>📋 {quiz.QuestionsCount} вопросов</span>
                    <span>⭐ {quiz.TotalPoints} баллов</span>
                    <span>🎮 {quiz.TimesPlayed} игр</span>
                    <span>📊 Средний балл: {quiz.AverageScore}%</span>
                </div>
                <div class='quiz-creator'>👑 Автор: Админко</div>
            </div>
        </a>";
    }

    //получаем пользовательские викторинки
    var customquizzes = await db.Quizzes
        .Include(q => q.Creator)
        .Where(q => q.CreatorId != adminId)
        .ToListAsync();

    //формируем их карточки
    string customquizzesHtml = "";
    foreach (var quiz in customquizzes)
    {
        string creatorName = quiz.Creator.Login;

        customquizzesHtml += $@"
        <a href='/quiz/{quiz.Id}' class='quiz-card-link'>
            <div class='quiz-card'>
                <div class='quiz-badge user'>👤 Пользовательская</div>
                <h3>{quiz.Title}</h3>
                <p>{quiz.Description ?? "Описание отсутствует"}</p>
                <div class='quiz-stats'>
                    <span>📋 {quiz.QuestionsCount} вопросов</span>
                    <span>⭐ {quiz.TotalPoints} баллов</span>
                    <span>🎮 {quiz.TimesPlayed} игр</span>
                </div>
                <div class='quiz-creator'>
                    ✍️ Автор: {creatorName}
                </div>
            </div>
        </a>";
    }

    //если нет пользовательских викторин
    if (string.IsNullOrEmpty(customquizzesHtml))
    {
        customquizzesHtml = $@"
        <div class='empty-state'>
            <p>😔 Пока нет пользовательских викторин</p>
            <p style='margin-top: 10px; font-size: 0.9em;'>Станьте первым создателем!</p>
        </div>";
    }
    string html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Викторины</title>
        <meta charset='UTF-8'>
        <style>
            * {{
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }}

            body {{
                font-family: 'Segoe UI', 'Inter', system-ui, -apple-system, BlinkMacSystemFont, 'Roboto', sans-serif;
                background: linear-gradient(135deg, #ffe6f0 0%, #fff0f5 100%);
                color: #2d2d2d;
                padding: 2rem;
                min-height: 100vh;
            }}

            .container {{
                max-width: 1300px;
                margin: 0 auto;
                background: rgba(255, 255, 255, 0.92);
                backdrop-filter: blur(10px);
                border-radius: 2rem;
                padding: 2rem;
                box-shadow: 0 25px 45px -12px rgba(255, 105, 180, 0.3);
                border: 1px solid rgba(255, 255, 255, 0.4);
            }}

            /* Заголовки */
            h1 {{
                font-size: 2.2rem;
                font-weight: 700;
                background: linear-gradient(135deg, #ff6d9e, #ffb6c1);
                background-clip: text;
                -webkit-background-clip: text;
                color: transparent;
                margin-bottom: 1.5rem;
                border-bottom: 3px solid #ffb6c1;
                display: inline-block;
                padding-bottom: 0.3rem;
            }}

            h2 {{
                font-size: 1.6rem;
                font-weight: 600;
                color: #ff6d9e;
                margin: 2rem 0 1rem 0;
                padding-bottom: 0.5rem;
                border-bottom: 2px solid rgba(255, 109, 158, 0.3);
            }}

            /* Навигация */
            .nav-buttons {{
                text-align: center;
                margin-bottom: 2rem;
                padding-bottom: 1rem;
                border-bottom: 1px solid rgba(255, 109, 158, 0.2);
                display: flex;
                gap: 0.8rem;
                justify-content: center;
                flex-wrap: wrap;
            }}

            .nav-btn {{
                background: white;
                color: #ff6d9e;
                text-decoration: none;
                padding: 0.6rem 1.2rem;
                border-radius: 60px;
                font-weight: 500;
                transition: all 0.3s ease;
                border: 1px solid #ffb6c1;
                backdrop-filter: blur(5px);
            }}

            .nav-btn:hover {{
                background: linear-gradient(135deg, #ff6d9e, #ff8eb5);
                color: white;
                transform: translateY(-2px);
                border-color: transparent;
                box-shadow: 0 5px 15px rgba(255, 109, 158, 0.3);
            }}

            /* Кнопки */
            button, .button {{
                background: white;
                color: #ff6d9e;
                border: 1.5px solid #ffb6c1;
                padding: 0.6rem 1.2rem;
                border-radius: 60px;
                cursor: pointer;
                font-family: inherit;
                font-size: 0.9rem;
                font-weight: 500;
                transition: all 0.3s ease;
                text-decoration: none;
                display: inline-block;
            }}

            button:hover, .button:hover {{
                background: linear-gradient(135deg, #ff6d9e, #ff8eb5);
                border-color: transparent;
                color: white;
                transform: translateY(-2px);
                box-shadow: 0 5px 12px rgba(255, 109, 158, 0.3);
            }}

            /* Формы */
            input, textarea, select {{
                width: 100%;
                padding: 0.7rem;
                margin: 0.3rem 0 1rem 0;
                border: 1.5px solid #ffd0dc;
                border-radius: 1rem;
                font-family: inherit;
                background: white;
                transition: all 0.3s ease;
            }}

            input:focus, textarea:focus, select:focus {{
                outline: none;
                border-color: #ff6d9e;
                box-shadow: 0 0 0 3px rgba(255, 109, 158, 0.1);
            }}

            label {{
                font-weight: 600;
                color: #ff6d9e;
                margin-top: 0.5rem;
                display: block;
            }}

            /* Карточки викторин */
            .quiz-grid {{
                display: grid;
                grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
                gap: 1.5rem;
                margin: 1.5rem 0;
            }}

            .quiz-card {{
                background: white;
                border-radius: 1.5rem;
                padding: 1.5rem;
                transition: all 0.3s cubic-bezier(0.53, 0.01, 0.35, 1.5);
                border: 1px solid rgba(255, 109, 158, 0.2);
                box-shadow: 0 5px 15px rgba(0,0,0,0.02);
                height: 100%;
                display: flex;
                flex-direction: column;
            }}

            .quiz-card:hover {{
                transform: translateY(-8px);
                border-color: #ff6d9e;
                box-shadow: 0 20px 30px -12px rgba(255, 109, 158, 0.25);
            }}

            .quiz-card-link {{
                text-decoration: none;
                color: inherit;
            }}

            .quiz-card h3 {{
                font-size: 1.3rem;
                color: #ff6d9e;
                margin-bottom: 0.5rem;
                font-weight: 600;
            }}

            .quiz-card p {{
                color: #666;
                font-size: 0.9rem;
                margin-bottom: 1rem;
                line-height: 1.4;
                flex-grow: 1;
            }}

            .quiz-badge {{
                display: inline-block;
                padding: 0.2rem 0.8rem;
                border-radius: 60px;
                font-size: 0.7rem;
                font-weight: 600;
                margin-bottom: 0.8rem;
                width: fit-content;
            }}

            .quiz-badge.official {{
                background: linear-gradient(135deg, #ff6d9e, #ffb6c1);
                color: white;
            }}

            .quiz-badge.user {{
                background: #f0f0f0;
                color: #ff6d9e;
                border: 1px solid #ffb6c1;
            }}

            .quiz-stats {{
                display: flex;
                flex-wrap: wrap;
                gap: 0.5rem;
                margin: 0.8rem 0;
                font-size: 0.75rem;
                color: #888;
            }}

            .quiz-stats span {{
                background: rgba(255, 109, 158, 0.05);
                padding: 0.2rem 0.6rem;
                border-radius: 60px;
            }}

            .quiz-creator {{
                margin-top: 0.8rem;
                padding-top: 0.8rem;
                border-top: 1px solid #ffe0e8;
                font-size: 0.75rem;
                color: #ff9eb5;
                font-weight: 500;
            }}

            .custom-quizzes {{
                margin-top: 2rem;
            }}

            /* Блоки вопросов */
            .question-block {{
                background: white;
                border-radius: 1.5rem;
                padding: 1.5rem;
                margin-bottom: 1.5rem;
                border: 1px solid rgba(255, 109, 158, 0.15);
                transition: all 0.3s ease;
            }}

            .question-block:hover {{
                border-color: #ffb6c1;
                box-shadow: 0 5px 15px rgba(255, 109, 158, 0.05);
            }}

            .question-header {{
                display: flex;
                justify-content: space-between;
                margin-bottom: 1rem;
                padding-bottom: 0.5rem;
                border-bottom: 1px solid #ffe0e8;
            }}

            .question-num {{
                font-weight: 700;
                color: #ff6d9e;
            }}

            .question-points {{
                font-weight: 700;
                color: #ffb6c1;
            }}

            .question-text {{
                margin-bottom: 1.2rem;
                font-size: 1.05rem;
                font-weight: 500;
                color: #2d2d2d;
            }}

            .answers {{
                display: flex;
                flex-direction: column;
                gap: 0.8rem;
            }}

            .answer-option {{
                display: flex;
                align-items: center;
                gap: 0.8rem;
                padding: 0.6rem;
                border-radius: 1rem;
                background: #fefafc;
                border: 1px solid #ffe0e8;
                cursor: pointer;
                transition: all 0.2s ease;
            }}

            .answer-option:hover {{
                background: #fff5f8;
                border-color: #ffb6c1;
            }}

            input[type='radio'] {{
                width: 1.2rem;
                height: 1.2rem;
                accent-color: #ff6d9e;
                margin: 0;
            }}

            /* Результат */
            .result-box {{
                background: white;
                border-radius: 1.5rem;
                padding: 2rem;
                text-align: center;
                margin: 2rem 0;
                border: 2px solid #ffb6c1;
            }}

            .score {{
                font-size: 3rem;
                font-weight: 800;
                background: linear-gradient(135deg, #ff6d9e, #ffb6c1);
                background-clip: text;
                -webkit-background-clip: text;
                color: transparent;
                margin: 1rem 0;
            }}

            /* Пустое состояние */
            .empty-state {{
                background: rgba(255, 109, 158, 0.05);
                border-radius: 1.5rem;
                padding: 3rem;
                text-align: center;
                color: #ff9eb5;
                border: 1px dashed #ffb6c1;
            }}

            /* Форма создания викторины */
            .form-group {{
                margin-bottom: 1.5rem;
            }}

            /* Профиль */
            .info-card {{
                background: white;
                border-radius: 1.5rem;
                padding: 1.5rem;
                margin: 1.5rem 0;
                border: 1px solid rgba(255, 109, 158, 0.2);
            }}

            .info-item {{
                margin: 0.8rem 0;
                padding: 0.5rem 0;
                border-bottom: 1px solid #ffe0e8;
            }}

            .info-label {{
                font-weight: 700;
                color: #ff6d9e;
                width: 150px;
                display: inline-block;
            }}

            .button-group {{
                display: flex;
                gap: 1rem;
                margin-top: 1.5rem;
                flex-wrap: wrap;
            }}

            @media (max-width: 768px) {{
                body {{ padding: 1rem; }}
                .container {{ padding: 1rem; }}
                h1 {{ font-size: 1.6rem; }}
                .quiz-grid {{ grid-template-columns: 1fr; }}
                .button-group {{ flex-direction: column; }}
                .info-label {{ width: 100%; display: block; margin-bottom: 0.3rem; }}
            }}
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='nav-buttons'>
                <a href='/' class='nav-btn'>🏠 Главная</a>
                <a href='/leaderboard' class='nav-btn'>🏆 Список лидеров</a>
                <a href='/profile' class='nav-btn'>👤 Профиль</a>
            </div>
            
            <h1>📚 Все викторины</h1>
            
            <div class='quiz-grid'>
                {defquizzesHtml}
            </div>
            <div class='custom-quizzes'>
                <h2>✨ Пользовательские викторины</h2>
                <div class='quiz-grid'>
                    {customquizzesHtml}
                </div>
            </div>
        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

//прохождение викторинки
app.MapGet("/quiz/{id:int}", async (int id, HttpContext context, AppDbContext db) =>
{
    string userId = context.User.FindFirstValue("UserId");

    var quiz = await db.Quizzes  //достаем викторину
        .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
        .FirstOrDefaultAsync(q => q.Id == id);

    if (quiz == null) //если не нашли - показываем ошибку
        return Results.NotFound("Викторина не найдена");

    //делаем вопросы и ответы(потом все добавим в общ HTML)
    string questionsHtml = "";
    foreach (var question in quiz.Questions.OrderBy(q => q.OrderNum))
    {
        string answersHtml = "";
        foreach (var answer in question.Answers.OrderBy(a => a.OrderNum))
        {
            answersHtml += $@"
            <label class='answer-option'>
                <input type='radio' name='q{question.Id}' value='{answer.Id}' required>
                <span>{answer.Text}</span>
            </label>";
        }

        questionsHtml += $@"
        <div class='question-block'>
            <div class='question-header'>
                <span class='question-num'>📌 Вопрос {question.OrderNum}</span>
                <span class='question-points'>⭐ {question.Points} очков</span>
            </div>
            <div class='question-text'>{question.Text}</div>
            <div class='answers'>
                {answersHtml}
            </div>
        </div>";
    }

    string html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>{quiz.Title}</title>
        <meta charset='UTF-8'>
        <style>
            * {{
                margin: 0;
                padding: 0;
                box-sizing: border-box;
            }}
            body {{
                font-family: 'Segoe UI', 'Inter', system-ui, -apple-system, BlinkMacSystemFont, 'Roboto', sans-serif;
                background: linear-gradient(135deg, #ffe6f0 0%, #fff0f5 100%);
                color: #2d2d2d;
                padding: 2rem;
                min-height: 100vh;
            }}
            .container {{
                max-width: 900px;
                margin: 0 auto;
                background: rgba(255, 255, 255, 0.92);
                backdrop-filter: blur(10px);
                border-radius: 2rem;
                padding: 2rem;
                box-shadow: 0 25px 45px -12px rgba(255, 105, 180, 0.3);
                border: 1px solid rgba(255, 255, 255, 0.4);
            }}
            .quiz-header {{
                background: linear-gradient(135deg, rgba(255, 109, 158, 0.1), rgba(255, 182, 193, 0.1));
                border-radius: 1.5rem;
                padding: 2rem;
                margin-bottom: 2rem;
                text-align: center;
                border: 1px solid rgba(255, 109, 158, 0.2);
            }}
            h1 {{
                font-size: 2rem;
                font-weight: 700;
                background: linear-gradient(135deg, #ff6d9e, #ffb6c1);
                background-clip: text;
                -webkit-background-clip: text;
                color: transparent;
                margin-bottom: 0.8rem;
            }}
            .quiz-header p {{
                color: #666;
                margin: 0.5rem 0;
            }}
            .question-block {{
                background: white;
                border-radius: 1.5rem;
                padding: 1.5rem;
                margin-bottom: 1.5rem;
                border: 1px solid rgba(255, 109, 158, 0.15);
                transition: all 0.3s ease;
            }}
            .question-block:hover {{
                border-color: #ffb6c1;
                box-shadow: 0 5px 15px rgba(255, 109, 158, 0.05);
            }}
            .question-header {{
                display: flex;
                justify-content: space-between;
                margin-bottom: 1rem;
                padding-bottom: 0.5rem;
                border-bottom: 1px solid #ffe0e8;
            }}
            .question-num {{
                font-weight: 700;
                color: #ff6d9e;
            }}
            .question-points {{
                font-weight: 700;
                color: #ffb6c1;
            }}
            .question-text {{
                margin-bottom: 1.2rem;
                font-size: 1.05rem;
                font-weight: 500;
                color: #2d2d2d;
            }}
            .answers {{
                display: flex;
                flex-direction: column;
                gap: 0.8rem;
            }}
            .answer-option {{
                display: flex;
                align-items: center;
                gap: 0.8rem;
                padding: 0.6rem;
                border-radius: 1rem;
                background: #fefafc;
                border: 1px solid #ffe0e8;
                cursor: pointer;
                transition: all 0.2s ease;
            }}
            .answer-option:hover {{
                background: #fff5f8;
                border-color: #ffb6c1;
                transform: translateX(5px);
            }}
            input[type='radio'] {{
                width: 1.2rem;
                height: 1.2rem;
                accent-color: #ff6d9e;
                margin: 0;
            }}
            .submit-btn {{
                background: linear-gradient(135deg, #ff6d9e, #ff8eb5);
                color: white;
                border: none;
                padding: 0.8rem 1.5rem;
                font-size: 1rem;
                font-weight: 600;
                cursor: pointer;
                width: 100%;
                margin-top: 1.5rem;
                border-radius: 60px;
                transition: all 0.3s ease;
            }}
            .submit-btn:hover {{
                transform: translateY(-2px);
                box-shadow: 0 10px 20px -8px #ff6d9e;
            }}
            .nav-btn {{
                background: white;
                color: #ff6d9e;
                text-decoration: none;
                padding: 0.6rem 1.2rem;
                border-radius: 60px;
                display: inline-block;
                margin-bottom: 1.5rem;
                font-weight: 500;
                transition: all 0.3s ease;
                border: 1px solid #ffb6c1;
            }}
            .nav-btn:hover {{
                background: linear-gradient(135deg, #ff6d9e, #ff8eb5);
                color: white;
                transform: translateY(-2px);
                border-color: transparent;
            }}
            @media (max-width: 600px) {{
                body {{ padding: 1rem; }}
                .container {{ padding: 1rem; }}
                .quiz-header {{ padding: 1rem; }}
                h1 {{ font-size: 1.5rem; }}
            }}
        </style>
    </head>
    <body>
        <div class='container'>
            <a href='/quizzes' class='nav-btn'>← Назад к викторинам</a>
            
            <div class='quiz-header'>
                <h1>📝 {quiz.Title}</h1>
                <p>{quiz.Description ?? "Нет описания"}</p>
                <p style='color: #ff6d9e; font-weight: 600;'>🏆 Максимум баллов: {quiz.TotalPoints}</p>
            </div>
            
            <form method='post' action='/quiz/{quiz.Id}/submit'>
                {questionsHtml}
                <button type='submit' class='submit-btn'>✅ Завершить викторину</button>
            </form>
        </div>
    </body>
    </html>";

    return Results.Content(html, "text/html; charset=utf-8");
});

//обработчик
app.MapPost("/quiz/{id:int}/submit", async (int id, HttpContext context, AppDbContext db) =>
{
    string userId = context.User.FindFirstValue("UserId");

    int currentUserId = int.Parse(userId);
    var form = await context.Request.ReadFormAsync();

    //загружаем викторинко
    var quiz = await db.Quizzes
        .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
        .FirstOrDefaultAsync(q => q.Id == id);

    if (quiz == null)
        return Results.NotFound("НЕту");

    int totalScore = 0;
    var userAnswers = new List<UserAnswer>();

    //подсчёт баллов
    foreach (var question in quiz.Questions)
    {
        string answerKey = $"q{question.Id}";
        string selectedAnswerId = form[answerKey];

        if (!string.IsNullOrEmpty(selectedAnswerId) && int.TryParse(selectedAnswerId, out int answerId))
        {
            var selectedAnswer = question.Answers.FirstOrDefault(a => a.Id == answerId);
            bool isCorrect = selectedAnswer?.IsCorrect ?? false;

            if (isCorrect)
            {
                totalScore += question.Points;
            }

            userAnswers.Add(new UserAnswer
            {
                QuestionId = question.Id,
                AnswerId = answerId,
                IsCorrect = isCorrect
            });
        }
    }

    //сохраняем результат
    var result = new Result
    {
        UserId = currentUserId,
        QuizId = id,
        Score = totalScore,
        MaxScore = quiz.TotalPoints,
        Percentage = quiz.TotalPoints > 0 ? (totalScore * 100.0m / quiz.TotalPoints) : 0
    };

    db.Results.Add(result);
    await db.SaveChangesAsync();

    //сохраняем ответы
    foreach (var ua in userAnswers)
    {
        ua.ResultId = result.Id;
        db.UserAnswers.Add(ua);
    }
    await db.SaveChangesAsync();

    //обновляем статисктику пользваотеля
    var user = await db.Users.FindAsync(currentUserId);
    if (user != null)
    {
        user.QuizzesPassed++;
        user.TotalPoints += totalScore;
        if (totalScore > user.BestScore)
            user.BestScore = totalScore;
        await db.SaveChangesAsync();
    }

    //обновляем статисику викторины
    quiz.TimesPlayed++;

    var allResults = await db.Results
        .Where(r => r.QuizId == id)
        .ToListAsync();

    if (allResults.Any())
    {
        quiz.AverageScore = allResults.Average(r => r.Percentage ?? 0);
    }
    await db.SaveChangesAsync();

    //показваем пользователю результат
    int percentage = quiz.TotalPoints > 0 ? (totalScore * 100 / quiz.TotalPoints) : 0;
    string emoji = percentage >= 80 ? "🎉" : percentage >= 50 ? "👍" : "📚";

    string resultHtml = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Результат викторины</title>
        <meta charset='UTF-8'>
        <style>
            * {{ margin: 0; padding: 0; box-sizing: border-box; }}
            body {{
                font-family: 'Segoe UI', 'Inter', system-ui, sans-serif;
                background: linear-gradient(135deg, #ffe6f0 0%, #fff0f5 100%);
                color: #2d2d2d;
                min-height: 100vh;
                display: flex;
                align-items: center;
                justify-content: center;
                padding: 1.5rem;
            }}
            .container {{
                background: rgba(255, 255, 255, 0.95);
                backdrop-filter: blur(10px);
                border-radius: 2rem;
                padding: 2.5rem;
                max-width: 550px;
                width: 100%;
                text-align: center;
                box-shadow: 0 25px 45px -12px rgba(255, 105, 180, 0.3);
                border: 1px solid rgba(255, 255, 255, 0.4);
            }}
            h1 {{
                font-size: 2rem;
                font-weight: 700;
                background: linear-gradient(135deg, #ff6d9e, #ffb6c1);
                background-clip: text;
                -webkit-background-clip: text;
                color: transparent;
                margin-bottom: 1.5rem;
            }}
            .score {{
                font-size: 3.5rem;
                font-weight: 800;
                background: linear-gradient(135deg, #ff6d9e, #ffb6c1);
                background-clip: text;
                -webkit-background-clip: text;
                color: transparent;
                margin: 1rem 0;
            }}
            .percentage {{
                font-size: 1.5rem;
                color: #ff9eb5;
                margin: 0.5rem 0;
                font-weight: 600;
            }}
            .status {{
                margin: 1.5rem 0;
                padding: 0.8rem;
                background: rgba(255, 109, 158, 0.1);
                border-radius: 1rem;
                display: inline-block;
                font-size: 2rem;
            }}
            .btn {{
                display: inline-block;
                background: white;
                color: #ff6d9e;
                text-decoration: none;
                padding: 0.7rem 1.5rem;
                margin: 0.5rem;
                border-radius: 60px;
                font-weight: 600;
                transition: all 0.3s ease;
                border: 1.5px solid #ffb6c1;
            }}
            .btn:hover {{
                background: linear-gradient(135deg, #ff6d9e, #ff8eb5);
                border-color: transparent;
                color: white;
                transform: translateY(-2px);
            }}
            .button-group {{
                margin-top: 1.5rem;
            }}
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='status'>{emoji}</div>
            <h1>Результат викторины</h1>
            <div class='score'>{totalScore} / {quiz.TotalPoints}</div>
            <div class='percentage'>({percentage}%)</div>
            <div class='button-group'>
                <a href='/' class='btn'>🏠 На главную</a>
                <a href='/profile' class='btn'>👤 Профиль</a>
                <a href='/quizzes' class='btn'>📚 Другие викторины</a>
            </div>
        </div>
    </body>
    </html>";

    return Results.Content(resultHtml, "text/html; charset=utf-8");
});

app.MapGet("/leaderboard", async (HttpContext context, AppDbContext db) => //таблица лидеров(выбрать параметр нужно)
{
    string userId = context.User.FindFirstValue("UserId");

    string resultHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Leaderboard</title>
    <meta charset='UTF-8'>
        <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <link rel='stylesheet' href='/css/style.css'>
</head>
<body>
    <div class='container'>
        <h1>Список лидеров/рейтинрг</h1>
        
        <div class='filters'>
            <div class='filter-group'>
                <label>Выберите параметр:</label>
                <select id='sortSelect'>
                    <option value='totalscore'>Общее число баллов</option>
                    <option value='quizzespassed'>Количество пройденных викторин</option>
                    <option value='quizzescreated'>Количество созданных викторин</option>
                </select>
            </div>
            <div class='filter-group'>
                <button id='loadBtn'>Показать рейтинг</button>
            </div>
        </div>
        
        <div id='leaderboardContainer'>
            <div class='loading'>Выберите параметр</div>
    </div>

    <div>
        <a href = '/' class= 'btn' > На главную </a>
        <a href = '/profile' class= 'btn' > Профиль </a>
    </div>
</div>


<script>
        const sortSelect = document.getElementById('sortSelect');
        const loadBtn = document.getElementById('loadBtn');
        const container = document.getElementById('leaderboardContainer');

        async function loadLeaderboard()
        {{
            const sortBy = sortSelect.value;

            try
            {{
                const response = await fetch('/api/leaderboard?sort=' + sortBy);
                const data = await response.json();

                if (data.length === 0)
                {{
                    container.innerHTML = '<div class=""loading"">Ошибко. Пользователи не найдены</div>';
                    return;
                }}

                let html = '<table><thead><tr>';
                html += '<th>#</th>';
                html += '<th>Пользователь</th>';
                html += '<th>Всего баллов</th>';
                html += '<th>Пройдено викторин</th>';
                html += '<th>Создано викторин</th>';
                html += '</tr></thead><tbody>';

                for (let i = 0; i < data.length; i++)
                {{
                    const user = data[i];
                    html += '<tr>';
                    html += '<td>' + user.rank + '</td>';
                    html += '<td>' + escapeHtml(user.login) + '</td>';
                    html += '<td>' + user.totalPoints + '</td>';
                    html += '<td>' + user.quizzesPassed + '</td>';
                    html += '<td>' + user.quizzesCreated + '</td>';
                    html += '</tr>';
                }}

                html += '</tbody></table>';
                html += '<div style=""margin-top: 20px; font-size: 0.9em; color: #666;"">';
                html += '</div>';

                container.innerHTML = html;
            }}
            catch (error)
            {{
                console.error('Ошибка:', error);
                container.innerHTML = '<div class=""loading"">Ошибко загрузки рейтнг</div>';
            }}
        }}

        function escapeHtml(text)
        {{
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }}

        loadBtn.addEventListener('click', loadLeaderboard);
    </script>
    </body>
</html> ";
    return Results.Content(resultHtml, "text/html; charset=utf-8");
});
//загружеем саму таблицу
app.MapGet("/api/leaderboard", async (AppDbContext db, string sort = "totalscore") =>
{
    IQueryable<User> usersQuery = db.Users.Where(u => u.Login != "admin");

    switch (sort)
    {
        case "totalscore":
            usersQuery = usersQuery.OrderByDescending(u => u.TotalPoints);
            break;
        case "quizzespassed":
            usersQuery = usersQuery.OrderByDescending(u => u.QuizzesPassed);
            break;
        case "quizzescreated":
            usersQuery = usersQuery.OrderByDescending(u => u.QuizzesCreated);
            break;
        default:
            usersQuery = usersQuery.OrderByDescending(u => u.TotalPoints);
            break;
    }

    var users = await usersQuery.ToListAsync();

    var leaderboardData = new List<object>();
    int rank = 1;

    foreach (var user in users)
    {
        leaderboardData.Add(new
        {
            rank = rank++,
            login = user.Login,
            totalPoints = user.TotalPoints,
            quizzesPassed = user.QuizzesPassed,
            quizzesCreated = user.QuizzesCreated
        });
    }

    return Results.Json(leaderboardData);
});
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
}
