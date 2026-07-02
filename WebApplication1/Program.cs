using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7011", "http://localhost:5121")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Строка подключения не найдена.");

var databaseConnectionString = BuildPostgresConnectionString(rawConnectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("your-secret-key-minimum-32-characters-long-123456789"))
        };
    });

builder.Services.AddAuthorization();
// HttpClient для GigaChat.
// ВАЖНО: у серверов GigaChat сертификат НУЦ Минцифры, которого нет в доверенных по умолчанию.
// Для учебного проекта отключаем проверку SSL. В реальном проекте так делать нельзя —
// нужно установить корневой сертификат Минцифры.
builder.Services.AddHttpClient("GigaChat")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// Регистрируем сервис GigaChat как singleton, чтобы access token переиспользовался между запросами
builder.Services.AddSingleton<GigaChatService>();

builder.Services.AddScoped<IGamificationService, GamificationService>();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

//тест подключния к бд
app.MapGet("/api/test/db", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    if (canConnect)
    {
        var usersCount = await db.Users.CountAsync();
        return Results.Ok(new
        {
            success = true,
            message = "Подключение прошло успешно"
        });
    }
    else
    {
        return Results.Ok(new { success = false, message = "Не удалось подключиться" });
    }
});
//тест апи
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "OK",
    message = "Апи работает"
}));
//тест ии
app.MapGet("/api/test/gigachat", async (GigaChatService giga) =>
{
    try
    {
        string testPrompt = "Привет";
        string answer = await giga.AskAsync(testPrompt);

        return Results.Ok(new
        {
            success = true,
            message = "ИИ работает",
            testQuestion = testPrompt,
            testAnswer = answer
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            success = false,
            message = $"Ошибко нейронк: {ex.Message}"
        });
    }
});
//тест связи асп.нет и веб=-апи
app.MapGet("/test-ping", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("ApiClient");

    try
    {
        var response = await client.GetStringAsync("/api/health");
        return Results.Ok(new
        {
            success = true,
            message = "Связь с апи работает",
            apiResponse = response
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            success = false,
            message = $"Ошибка связи с апи: {ex.Message}"
        });
    }
});
//тест получаются ли викторины
app.MapGet("/api/test/quizzes", async (AppDbContext db) =>
{
    var quizzes = await db.Quizzes
        .Take(5)
        .Select(q => new { q.Id, q.Title, q.QuestionsCount })
        .ToListAsync();

    return Results.Ok(new
    {
        success = true,
        count = quizzes.Count,
        quizzes = quizzes,
        message = quizzes.Count > 0 ? "Викторины загружены" : "Викторин нет..."
    });
});
//метод для создания тестовой викторины(чтобы применить гет и использовать только браузер)
async Task<object> CreateTestQuizAsync(AppDbContext db, int creatorId)
{
    var quiz = new Quiz
    {
        Title = "тест",
        Description = "тест",
        CreatorId = creatorId,
        Difficulty = 3,
        TotalPoints = 0,
        QuestionsCount = 0,
        TimesPlayed = 0,
        AverageScore = 0
    };

    db.Quizzes.Add(quiz);
    await db.SaveChangesAsync();

    var questionsData = new[]
    {
        new {
            Text = "Вопрос1",
            Points = 10,
            Answers = new[] { ("нет", false), ("да", true), ("нет", false), ("нет", false) }
        },
        new {
            Text = "Вопрос2",
            Points = 10,
            Answers = new[] { ("нет", false), ("да", true), ("нет", false), ("нет", false) }
        }
    };

    int totalPoints = 0;
    int orderNum = 1;
    int questionsCount = 0;

    foreach (var qData in questionsData)
    {
        var question = new Question
        {
            QuizId = quiz.Id,
            Text = qData.Text,
            Points = qData.Points,
            OrderNum = orderNum++
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        int answerOrder = 1;
        foreach (var aData in qData.Answers)
        {
            var answer = new Answer
            {
                QuestionId = question.Id,
                Text = aData.Item1,
                IsCorrect = aData.Item2,
                OrderNum = answerOrder++
            };
            db.Answers.Add(answer);
        }

        await db.SaveChangesAsync();
        totalPoints += question.Points;
        questionsCount++;
    }

    quiz.TotalPoints = totalPoints;
    quiz.QuestionsCount = questionsCount;
    await db.SaveChangesAsync();

    var user = await db.Users.FindAsync(creatorId);
    if (user != null)
    {
        user.QuizzesCreated++;
        await db.SaveChangesAsync();
    }

    return new { quiz, user, questionsCount, totalPoints };
}
//тест сохранения викторины
app.MapGet("/api/test/save-quiz", async (AppDbContext db, HttpContext context) =>
{
    try
    {
        int creatorId = 1;
        var result = await CreateTestQuizAsync(db, creatorId);

        var quiz = result.GetType().GetProperty("quiz")?.GetValue(result) as Quiz;
        var user = result.GetType().GetProperty("user")?.GetValue(result) as User;
        int questionsCount = (int)result.GetType().GetProperty("questionsCount")?.GetValue(result)!;
        int totalPoints = (int)result.GetType().GetProperty("totalPoints")?.GetValue(result)!;

        string html = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='UTF-8'>
            <style>
            </style>
        </head>
        <body>
            <div class='container'>
                <h1>Результат</h1>
                <div class='success'><strong>Тест успешно выполнен</strong></div>
                <div class='info'>
                    <p><strong>ID викторины:</strong> {quiz?.Id}</p>
                    <p><strong>Название:</strong> {quiz?.Title}</p>
                    <p><strong>Вопросов:</strong> {questionsCount}</p>
                    <p><strong>Баллов:</strong> {totalPoints}</p>
                    <p><strong>Создатель:</strong> {user?.Login}</p>
                    <p><strong>Всего создано викторин:</strong> {user?.QuizzesCreated}</p>
                </div>
            </div>
        </body>
        </html>";

        return Results.Content(html, "text/html; charset=utf-8");
    }
    catch (Exception ex)
    {
        return Results.Content($@"
        <html>
            <head><meta charset='UTF-8'></head>
            <body style='font-family: Arial; text-align: center; padding: 50px;'>
                <h1 style='color: #ff4444;'>Ошибко: {ex.Message}</h1>
            </body>
        </html>", "text/html; charset=utf-8");
    }
});
app.MapPost("/api/auth/register", async (AppDbContext db, RegisterRequest request) =>
{

    var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Login == request.Login);
    if (existingUser != null)
        return Results.BadRequest(new { success = false, message = "Пользователь уже существует" });

    if (request.Password != request.ConfirmPassword)
        return Results.BadRequest(new { success = false, message = "Пароли не совпадают" });

    string hashedPassword = GetSha256Hash(request.Password);
    var user = new User
    {
        Login = request.Login,
        Password = hashedPassword,
        TotalPoints = 0,
        QuizzesPassed = 0,
        QuizzesCreated = 0,
        BestScore = 0
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();


    return Results.Ok(new { success = true, message = "Регистрация успешна" });
});
app.MapGet("/api/test/gamification", async (AppDbContext db, HttpContext context, IGamificationService service) =>
{
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null) return Results.Unauthorized();

    int userId = int.Parse(userIdClaim.Value);
    //var service = new GamificationService(db);
    await service.SeedAchievementsAsync();
    await service.AddXPAsync(userId, 50);
    await service.UpdateStreakAsync(userId);
    await service.CheckAchievementsAsync(userId);
    await service.UpdateLeagueAsync(userId);

    var user = await db.Users
        .Include(u => u.UserAchievements)
            .ThenInclude(ua => ua.Achievement)
        .FirstOrDefaultAsync(u => u.Id == userId);
    var achievements = new List<object>();
    if (user?.UserAchievements != null)
    {
        foreach (var ua in user.UserAchievements)
        {
            achievements.Add(new
            {
                ua.Achievement.Name,
                ua.Achievement.Description,
                ua.Achievement.Icon,
                ua.Achievement.XP,
                ua.UnlockedAt
            });
        }
    }
    return Results.Ok(new
    {
        XP = user?.XP ?? 0,
        Level = user?.Level ?? 1,
        Streak = user?.Streak ?? 0,
        League = user?.League ?? "Бронза",
        Achievements = achievements
    });
});
app.MapGet("/api/gamification", async (AppDbContext db, HttpContext context) =>
{
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null) return Results.Unauthorized();

    int userId = int.Parse(userIdClaim.Value);

    var user = await db.Users
        .Include(u => u.UserAchievements)
            .ThenInclude(ua => ua.Achievement)
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (user == null) return Results.NotFound();

    return Results.Ok(new
    {
        XP = user.XP,
        Level = user.Level,
        Streak = user.Streak,
        League = user.League,
        Achievements = user.UserAchievements.Select(ua => new
        {
            ua.Achievement.Name,
            ua.Achievement.Description,
            ua.Achievement.Icon,
            ua.Achievement.XP,
            ua.UnlockedAt
        })
    });
});
app.MapPost("/api/auth/login", async (AppDbContext db, LoginRequest request) =>
{

    string hashedPassword = GetSha256Hash(request.Password);
    var user = await db.Users.FirstOrDefaultAsync(u => u.Login == request.Login && u.Password == hashedPassword);

    if (user == null)
    {
        return Results.BadRequest(new { success = false, message = "Неверный логин или пароль" });
    }

    var token = GenerateJwtToken(user.Id, user.Login);


    return Results.Ok(new
    {
        success = true,
        message = "Вход выполнен",
        userId = user.Id,
        login = user.Login,
        token = token
    });
});

app.MapGet("/api/quizzes", async (AppDbContext db) =>
{
    var quizzes = await db.Quizzes
        .Include(q => q.Creator)
        .Select(q => new
        {
            q.Id,
            q.Title,
            q.Description,
            q.Difficulty,
            q.TotalPoints,
            q.QuestionsCount,
            q.TimesPlayed,
            q.AverageScore,
            CreatorId = q.CreatorId,
            CreatorName = q.Creator != null ? q.Creator.Login : "Unknown"
        })
        .ToListAsync();

    return Results.Ok(quizzes);
});

app.MapGet("/api/quizzes/{id}", async (int id, AppDbContext db) =>
{
    var quiz = await db.Quizzes
        .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
        .Where(q => q.Id == id)
        .Select(q => new
        {
            q.Id,
            q.Title,
            q.Description,
            q.Difficulty,
            q.TotalPoints,
            Questions = q.Questions.OrderBy(qs => qs.OrderNum).Select(qs => new
            {
                qs.Id,
                qs.Text,
                qs.Points,
                Answers = qs.Answers.OrderBy(a => a.OrderNum).Select(a => new
                {
                    a.Id,
                    a.Text,
                    a.IsCorrect
                })
            })
        })
        .FirstOrDefaultAsync();

    if (quiz == null)
        return Results.NotFound(new { message = "Викторина не найдена" });

    return Results.Ok(quiz);
});

app.MapPost("/api/quizzes", async (AppDbContext db, CreateQuizRequest request, HttpContext context) =>
{
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null)
    {
        return Results.Unauthorized();
    }

    int creatorId = int.Parse(userIdClaim.Value);

    var quiz = new Quiz
    {
        Title = request.Title,
        Description = request.Description ?? "",
        CreatorId = creatorId,
        Difficulty = request.Difficulty,
        TotalPoints = 0,
        QuestionsCount = 0,
        TimesPlayed = 0,
        AverageScore = 0
    };

    db.Quizzes.Add(quiz);
    await db.SaveChangesAsync();

    int totalPoints = 0;
    int questionsCount = 0;

    foreach (var qRequest in request.Questions)
    {

        var question = new Question
        {
            QuizId = quiz.Id,
            Text = qRequest.Text,
            Points = qRequest.Points > 0 ? qRequest.Points : 10,
            OrderNum = questionsCount + 1
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        int answerOrder = 1;
        foreach (var aRequest in qRequest.Answers)
        {

            var answer = new Answer
            {
                QuestionId = question.Id,
                Text = aRequest.Text,
                IsCorrect = aRequest.IsCorrect,
                OrderNum = answerOrder++
            };

            db.Answers.Add(answer);
        }

        await db.SaveChangesAsync();

        totalPoints += question.Points;
        questionsCount++;
    }

    quiz.TotalPoints = totalPoints;
    quiz.QuestionsCount = questionsCount;
    await db.SaveChangesAsync();

    var user = await db.Users.FindAsync(creatorId);
    if (user != null)
    {
        user.QuizzesCreated++;
        await db.SaveChangesAsync();
    }


    return Results.Ok(new { id = quiz.Id, message = "Викторина создана" });
});
app.MapPost("/api/quizzes/{id}/submit", async (int id, AppDbContext db, SubmitQuizRequest request, HttpContext context) =>
{
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null) return Results.Unauthorized();

    int currentUserId = int.Parse(userIdClaim.Value);

    var quiz = await db.Quizzes
        .Include(q => q.Questions)
            .ThenInclude(q => q.Answers)
        .FirstOrDefaultAsync(q => q.Id == id);

    if (quiz == null) return Results.NotFound(new { message = "Викторина не найдена" });

    int totalScore = 0;
    foreach (var answer in request.Answers)
    {
        var question = quiz.Questions.FirstOrDefault(q => q.Id == answer.QuestionId);
        if (question != null)
        {
            var selectedAnswer = question.Answers.FirstOrDefault(a => a.Id == answer.AnswerId);
            if (selectedAnswer != null && selectedAnswer.IsCorrect)
            {
                totalScore += question.Points;
            }
        }
    }

    var result = new Result
    {
        UserId = currentUserId,
        QuizId = id,
        Score = totalScore,
        MaxScore = quiz.TotalPoints,
        Percentage = quiz.TotalPoints > 0 ? (totalScore * 100.0m / quiz.TotalPoints) : 0
    };
    db.Results.Add(result);

    var user = await db.Users.FindAsync(currentUserId);
    if (user != null)
    {
        user.QuizzesPassed++;
        user.TotalPoints += totalScore;
        if (totalScore > user.BestScore)
            user.BestScore = totalScore;
    }

    quiz.TimesPlayed++;
    await db.SaveChangesAsync();

    var gamification = new GamificationService(db);

    int xpForQuiz = totalScore;
    await gamification.AddXPAsync(currentUserId, xpForQuiz);
    Console.WriteLine($"Начислено XP: {xpForQuiz} пользователю {currentUserId}");

    await gamification.UpdateStreakAsync(currentUserId);
    Console.WriteLine($"Стрик обновлён для пользователя {currentUserId}");

    var newAchievements = await gamification.CheckAchievementsAsync(currentUserId);
    Console.WriteLine($"Новых ачивок: {newAchievements?.Count ?? 0}");

    await gamification.UpdateLeagueAsync(currentUserId);
    Console.WriteLine($"Лига обновлена для пользователя {currentUserId}");

    user = await db.Users.FindAsync(currentUserId);

    return Results.Ok(new SubmitResultDto
    {
        Score = totalScore,
        MaxScore = quiz.TotalPoints,
        Percentage = quiz.TotalPoints > 0 ? (totalScore * 100 / quiz.TotalPoints) : 0,
        Gamification = new GamificationData
        {
            XP = user?.XP ?? 0,
            Level = user?.Level ?? 1,
            Streak = user?.Streak ?? 0,
            League = user?.League ?? "Бронза",
            NewAchievements = newAchievements?.Select(a => new AchievementDto
            {
                Name = a.Name,
                Description = a.Description,
                Icon = a.Icon,
                XP = a.XP
            }).ToList() ?? new List<AchievementDto>()
        }
    });
});
app.MapDelete("/api/quizzes/{id}", async (int id, AppDbContext db, HttpContext context) =>
{
    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null)
        return Results.Unauthorized();

    int userId = int.Parse(userIdClaim.Value);
    bool isAdmin = userId == 1;

    var quiz = await db.Quizzes.FindAsync(id);
    if (quiz == null)
        return Results.NotFound(new { message = "Викторина не найдена" });

    if (!isAdmin && quiz.CreatorId != userId)
        return Results.Forbid();

    db.Quizzes.Remove(quiz);
    await db.SaveChangesAsync();

    return Results.Ok(new { success = true, message = "Викторина удалена" });
});

app.MapGet("/api/leaderboard", async (AppDbContext db, string sort = "totalscore") =>
{
    var users = db.Users.Where(u => u.Login != "admin");

    users = sort switch
    {
        "totalscore" => users.OrderByDescending(u => u.TotalPoints),
        "quizzespassed" => users.OrderByDescending(u => u.QuizzesPassed),
        "quizzescreated" => users.OrderByDescending(u => u.QuizzesCreated),
        _ => users.OrderByDescending(u => u.TotalPoints)
    };

    var usersList = await users.ToListAsync();

    var result = usersList.Select((u, index) => new
    {
        rank = index + 1,
        u.Login,
        u.TotalPoints,
        u.QuizzesPassed,
        u.QuizzesCreated
    });

    return Results.Ok(result);
});

app.MapGet("/api/profile", async (AppDbContext db, HttpContext context) =>
{

    var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

    if (userIdClaim == null)
    {
        return Results.Unauthorized();
    }

    int userId = int.Parse(userIdClaim.Value);

    var user = await db.Users.FindAsync(userId);
    if (user == null)
        return Results.NotFound();

    return Results.Ok(new
    {
        user.Id,
        user.Login,
        user.TotalPoints,
        user.QuizzesPassed,
        user.QuizzesCreated,
        user.BestScore
    });
});
app.MapPost("/api/gigachat/hint", async (GigaChatRequest request, GigaChatService giga) =>
{
    try
    {
        string prompt = $"Дай пожалуйста подсказку (на три предложения примерно) к вопросу викторины: \"{request.QuestionText}\". " +
                       $"Не давай прямой ответ, а только намекни, но достаточно понятно.";

        string answer = await giga.AskAsync(prompt);
        return Results.Ok(new GigaChatResponse { Hint = answer });
    }
    catch (Exception ex)
    {
        return Results.Problem("Ошибка GigaChat: " + ex.Message);
    }
});
app.MapPost("/api/gigachat/generate-quiz", async Task<IResult> (GenerateQuizRequest request, GigaChatService giga) =>
{
    try
    {
        int questionsCount = Math.Min(request.QuestionsCount, 10);
        int answersCount = Math.Min(request.AnswersCount, 5);

        string prompt = $@"
            Тема викторины: {request.Topic}

            Параметры:
            - Количество вопросов: {request.QuestionsCount}
            - Количество вариантов ответов: {request.AnswersCount}, только один правильный
            - Уровень сложности: {request.Difficulty}

            Учитывай сложность при создании вопросов:
            - Если сложность 'легкий' — вопросы должны быть простыми, базовыми
            - Если сложность 'средний' — вопросы должны быть умеренной сложности
            - Если сложность 'сложный' — вопросы должны быть трудными, требующими глубоких знаний

            Отвечай ТОЛЬКО в таком формате (каждый вопрос с новой строки):

            1. Текст вопроса
                - Вариант 1 (правильный)
                - Вариант 2
                - Вариант 3
                - Вариант 4

            2. Следующий вопрос
                - Вариант 1
                - Вариант 2 (правильный)
                - Вариант 3
            - Вариант 4

            И так далее. Помечай правильный ответ словом '(правильный)' рядом с вариантом, '(неправильный)' писать НЕЛЬЗЯ.
        ";

        string answer = await giga.AskAsync(prompt);
        var quiz = ParseTextToQuiz(answer, request);

        return Results.Ok(quiz);
    }
    catch (Exception ex)
    {
        return Results.Ok(CreateFallbackQuizResponse(request));
    }
});
GenerateQuizResponse ParseTextToQuiz(string text, GenerateQuizRequest request)
{
    var response = new GenerateQuizResponse
    {
        Success = true,
        Title = $"Викторина: {request.Topic}",
        Description = $"Интересная викторина на тему {request.Topic}",
        Difficulty = request.Difficulty switch { "легкий" => 1, "средний" => 3, "сложный" => 5, _ => 3 },
        Questions = new List<GeneratedQuestion>()
    };

    var lines = text.Split('\n');

    int currentQuestion = -1;
    var currentQuestionText = "";
    var currentAnswers = new List<GeneratedAnswer>();

    foreach (var line in lines)
    {
        string trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed)) continue;

        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\."))
        {
            if (currentQuestion >= 0 && currentAnswers.Count > 0)
            {
                if (!currentAnswers.Any(a => a.IsCorrect) && currentAnswers.Count > 0)
                    currentAnswers[0].IsCorrect = true;

                response.Questions.Add(new GeneratedQuestion
                {
                    Text = CleanText(currentQuestionText),
                    Points = 10,
                    Answers = currentAnswers.Take(request.AnswersCount).ToList()
                });
            }

            currentQuestion++;
            currentQuestionText = trimmed.Substring(trimmed.IndexOf('.') + 1).Trim();
            currentAnswers = new List<GeneratedAnswer>();
        }
        else if (trimmed.StartsWith("-") || trimmed.StartsWith("•") || trimmed.StartsWith("*"))
        {
            string answerText = trimmed.TrimStart('-', '•', '*').Trim();
            bool isCorrect = answerText.Contains("(правильный)") || answerText.Contains("(верный)") || answerText.Contains("правильный");

            answerText = answerText.Replace("(правильный)", "").Replace("(верный)", "").Trim();

            if (!string.IsNullOrEmpty(answerText))
            {
                currentAnswers.Add(new GeneratedAnswer
                {
                    Text = CleanText(answerText),
                    IsCorrect = isCorrect
                });
            }
        }
    }

    if (currentQuestion >= 0 && currentAnswers.Count > 0)
    {
        if (!currentAnswers.Any(a => a.IsCorrect) && currentAnswers.Count > 0)
            currentAnswers[0].IsCorrect = true;

        response.Questions.Add(new GeneratedQuestion
        {
            Text = CleanText(currentQuestionText),
            Points = 10,
            Answers = currentAnswers.Take(request.AnswersCount).ToList()
        });
    }
    foreach (var q in response.Questions)
    {
        q.Answers = q.Answers.Take(request.AnswersCount).ToList();

        if (!q.Answers.Any(a => a.IsCorrect) && q.Answers.Count > 0)
            q.Answers[0].IsCorrect = true;

        while (q.Answers.Count < request.AnswersCount)
        {
            q.Answers.Add(new GeneratedAnswer
            {
                Text = $"Вариант {q.Answers.Count + 1}",
                IsCorrect = false
            });
        }
    }

    return response;
}

GenerateQuizResponse CreateFallbackQuizResponse(GenerateQuizRequest request)
{
    var response = new GenerateQuizResponse
    {
        Success = true,
        Title = $"Викторина: {request.Topic}",
        Description = $"Викторина на тему {request.Topic} (сложность: {request.Difficulty})",
        Difficulty = request.Difficulty switch { "легкий" => 1, "средний" => 3, "сложный" => 5, _ => 3 },
        Questions = new List<GeneratedQuestion>()
    };

    for (int i = 1; i <= request.QuestionsCount; i++)
    {
        var question = new GeneratedQuestion
        {
            Text = $"Вопрос {i} по теме '{request.Topic}'?",
            Points = 10,
            Answers = new List<GeneratedAnswer>()
        };

        for (int j = 1; j <= request.AnswersCount; j++)
        {
            question.Answers.Add(new GeneratedAnswer
            {
                Text = $"Вариант ответа {j}",
                IsCorrect = (j == 1)
            });
        }

        response.Questions.Add(question);
    }

    return response;
}

string CleanText(string input)
{
    if (string.IsNullOrEmpty(input)) return "";
    return input
        .Replace("\"", "'")
        .Replace("\n", " ")
        .Replace("\r", " ")
        .Trim();
}

app.Run();


static string BuildPostgresConnectionString(string rawConnectionString)
{
    if (!rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
        !rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
    {
        return rawConnectionString;
    }

    var databaseUri = new Uri(rawConnectionString);
    var userInfoParts = databaseUri.UserInfo.Split(':', 2, StringSplitOptions.None);

    if (userInfoParts.Length != 2)
    {
        throw new InvalidOperationException("Не удалось разобрать логин и пароль из URI-строки подключения PostgreSQL.");
    }

    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = databaseUri.Host,
        Port = databaseUri.IsDefaultPort ? 5432 : databaseUri.Port,
        Database = databaseUri.AbsolutePath.Trim('/'),
        Username = Uri.UnescapeDataString(userInfoParts[0]),
        Password = Uri.UnescapeDataString(userInfoParts[1])
    };

    foreach (var queryPart in databaseUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var queryPartPieces = queryPart.Split('=', 2, StringSplitOptions.None);
        var key = Uri.UnescapeDataString(queryPartPieces[0]);
        var value = queryPartPieces.Length > 1
            ? Uri.UnescapeDataString(queryPartPieces[1])
            : string.Empty;

        if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
        {
            connectionStringBuilder["SSL Mode"] = value;
            continue;
        }

        if (key.Equals("channel_binding", StringComparison.OrdinalIgnoreCase))
        {
            connectionStringBuilder["Channel Binding"] = value;
            continue;
        }

        connectionStringBuilder[key] = value;
    }

    return connectionStringBuilder.ConnectionString;
}

static string GetSha256Hash(string input)
{
    using var sha256 = SHA256.Create();
    byte[] bytes = Encoding.UTF8.GetBytes(input);
    byte[] hashBytes = sha256.ComputeHash(bytes);
    var sb = new StringBuilder();
    foreach (byte b in hashBytes)
        sb.Append(b.ToString("x2"));
    return sb.ToString();
}

static string GenerateJwtToken(int userId, string login)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-secret-key-minimum-32-characters-long-123456789"));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Name, login)
    };

    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.Now.AddDays(30),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
public interface IGamificationService
{
    Task AddXPAsync(int userId, int amount);
    Task UpdateStreakAsync(int userId);
    Task UpdateLeagueAsync(int userId);
    Task<List<Achievement>> CheckAchievementsAsync(int userId);
    Task SeedAchievementsAsync();
}
public class GamificationService : IGamificationService
{
    private readonly AppDbContext _db;
    public GamificationService(AppDbContext db) { _db = db; }
    public int GetLevel(int xp) { return (int)Math.Floor(Math.Sqrt(xp / 10)) + 1; } 
    public int GetXPForNextLevel(int currentLevel) { return currentLevel * currentLevel * 10; }
    public async Task AddXPAsync(int userId, int amount) {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        user.XP += amount;
        user.Level = GetLevel(user.XP);
        await _db.SaveChangesAsync();
    }
    public async Task UpdateStreakAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        var today = DateTime.UtcNow.Date;
        if (user.LastQuizDate.Date == today)  return; 
        if (user.LastQuizDate.Date == today.AddDays(-1)) {
            user.Streak++;
            user.LastQuizDate = today;
            if (user.Streak >= 3) user.XP += 5 * user.Streak;
        }
        else {
            user.Streak = 1;
            user.LastQuizDate = today;
        }
        await _db.SaveChangesAsync();
    }

    public async Task UpdateLeagueAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        if (user.LeagueUpdatedAt > DateTime.UtcNow.AddMonths(-1)) return;

        var allUsers = await _db.Users.OrderByDescending(u => u.TotalPoints).ToListAsync();
        var rank = allUsers.IndexOf(user) + 1;

        if (rank <= 3) user.League = "Золото";
        else if (rank <= 10) user.League = "Серебро";
        else user.League = "Бронза";

        user.LeagueUpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task<List<Achievement>> CheckAchievementsAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.UserAchievements)
                .ThenInclude(ua => ua.Achievement)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new List<Achievement>();

        var unlockedIds = user.UserAchievements.Select(ua => ua.AchievementId).ToHashSet();
        var allAchievements = await _db.Achievements.ToListAsync();
        var newAchievements = new List<Achievement>();

        foreach (var achievement in allAchievements)
        {
            if (unlockedIds.Contains(achievement.Id)) continue;

            if (await IsAchievementUnlockedAsync(user, achievement))
            {
                user.UserAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = achievement.Id,
                    UnlockedAt = DateTime.UtcNow
                });

                newAchievements.Add(achievement);

                if (achievement.XP > 0)
                {
                    await AddXPAsync(userId, achievement.XP);
                }
            }
        }

        await _db.SaveChangesAsync();
        return newAchievements; 
    }

    private async Task<bool> IsAchievementUnlockedAsync(User user, Achievement achievement)
    {
        var condition = achievement.Condition;

        if (condition.StartsWith("quizzes_passed:"))
        {
            int required = int.Parse(condition.Split(':')[1]);
            return user.QuizzesPassed >= required;
        }

        if (condition.StartsWith("quiz_created:"))
        {
            int required = int.Parse(condition.Split(':')[1]);
            return user.QuizzesCreated >= required;
        }

        if (condition.StartsWith("streak:"))
        {
            int required = int.Parse(condition.Split(':')[1]);
            return user.Streak >= required;
        }

        return false;
    }
    public async Task SeedAchievementsAsync()
    {
        if (await _db.Achievements.AnyAsync()) return;

        var achievements = new[]
        {
            new Achievement { Name = "Первая викторина", Description = "Пройди первую викторину", Icon = "🎯", XP = 10, Condition = "quizzes_passed:1" },
            new Achievement { Name = "Знаток", Description = "Пройди 10 викторин", Icon = "🧠", XP = 50, Condition = "quizzes_passed:10" },
            new Achievement { Name = "Эксперт", Description = "Пройди 50 викторин", Icon = "🎓", XP = 100, Condition = "quizzes_passed:50" },
            new Achievement { Name = "Создатель", Description = "Создай свою первую викторину", Icon = "✍️", XP = 15, Condition = "quiz_created:1" },
            new Achievement { Name = "Стрик 3", Description = "Играй 3 дня подряд", Icon = "🔥", XP = 20, Condition = "streak:3" },
            new Achievement { Name = "Стрик 7", Description = "Играй 7 дней подряд", Icon = "⚡", XP = 50, Condition = "streak:7" }
        };

        _db.Achievements.AddRange(achievements);
        await _db.SaveChangesAsync();
    }
}

public record RegisterRequest(string Login, string Password, string ConfirmPassword);
public record LoginRequest(string Login, string Password);
public class CreateQuizRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Difficulty { get; set; }
    public List<QuestionRequest> Questions { get; set; } = new();
}

public class QuestionRequest
{
    public string Text { get; set; } = "";
    public int Points { get; set; }
    public List<AnswerRequest> Answers { get; set; } = new();
}

public class AnswerRequest
{
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}
public class SubmitQuizRequest
{
    public List<UserAnswerRequest> Answers { get; set; } = new();
    public int UsedHints { get; set; } = 0;
    public int TotalQuestions { get; set; } = 0;
}
public record UserAnswerRequest(int QuestionId, int AnswerId);

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



    public int XP { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int Streak { get; set; } = 0;
    public DateTime LastQuizDate { get; set; } = DateTime.MinValue;
    public string League { get; set; } = "Бронза";
    public DateTime LeagueUpdatedAt { get; set; } = DateTime.UtcNow;
    public List<UserAchievement> UserAchievements { get; set; } = new();
}
public class Achievement
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public int XP { get; set; } = 0;
    public string Condition { get; set; } = ""; 
}
public class AchievementDto
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public int XP { get; set; }
    public DateTime UnlockedAt { get; set; }
}

public class UserAchievement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int AchievementId { get; set; }
    public DateTime UnlockedAt { get; set; }

    public User User { get; set; } = null!;
    public Achievement Achievement { get; set; } = null!;
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
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<Result> Results { get; set; }
    public DbSet<UserAnswer> UserAnswers { get; set; }
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }
}
class GigaChatService
{
    private readonly IHttpClientFactory _factory;
    private readonly string _authorizationKey;
    private readonly string _scope;

    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public GigaChatService(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _authorizationKey = config["GigaChat:AuthorizationKey"]!;
        _scope = config["GigaChat:Scope"]!;
    }

    public async Task<string> AskAsync(string prompt)
    {
        string token = await GetAccessTokenAsync();

        var client = _factory.CreateClient("GigaChat");

        var requestBody = new
        {
            model = "GigaChat",
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");
        message.Headers.Add("Authorization", "Bearer " + token);
        message.Content = JsonContent.Create(requestBody);

        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string answer = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return answer;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _accessToken;

        var client = _factory.CreateClient("GigaChat");

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");
        message.Headers.Add("Authorization", "Basic " + _authorizationKey);
        message.Headers.Add("RqUID", Guid.NewGuid().ToString());
        message.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("scope", _scope)
        });

        var response = await client.SendAsync(message);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString();
        long expiresAtMs = root.GetProperty("expires_at").GetInt64();
        _tokenExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtMs).AddMinutes(-1);

        return _accessToken!;
    }
}

public class GigaChatRequest
{
    public string QuestionText { get; set; } = "";
}

public class GigaChatResponse
{
    public string Hint { get; set; } = "";
}

public class GenerateQuizRequest
{
    public string Topic { get; set; } = "";
    public int QuestionsCount { get; set; }
    public int AnswersCount { get; set; }
    public string Difficulty { get; set; } = "";
}

public class GenerateQuizResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Difficulty { get; set; }
    public List<GeneratedQuestion> Questions { get; set; } = new();
}

public class GeneratedQuestion
{
    public string Text { get; set; } = "";
    public int Points { get; set; }
    public List<GeneratedAnswer> Answers { get; set; } = new();
}

public class GeneratedAnswer
{
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public class GeneratedQuizData
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int Difficulty { get; set; }
    public List<GeneratedQuestionData>? Questions { get; set; }
}

public class GeneratedQuestionData
{
    public string? Text { get; set; }
    public int Points { get; set; }
    public List<GeneratedAnswerData>? Answers { get; set; }
}

public class GeneratedAnswerData
{
    public string? Text { get; set; }
    public bool IsCorrect { get; set; }
}
public class FallbackQuizData
{
    public List<FallbackQuestion>? Questions { get; set; }
}

public class FallbackQuestion
{
    public string? Text { get; set; }
    public List<FallbackAnswer>? Answers { get; set; }
}

public class FallbackAnswer
{
    public string? Text { get; set; }
    public bool IsCorrect { get; set; }
}
public class SubmitResultDto
{
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int Percentage { get; set; }
    public GamificationData Gamification { get; set; } = new();
}

public class GamificationData
{
    public int XP { get; set; }
    public int Level { get; set; }
    public int Streak { get; set; }
    public string League { get; set; } = "";
    public int HintPenalty { get; set; } = 0;
    public int FinalXP { get; set; } = 0;
    public int UsedHints { get; set; } = 0;
    public int MaxHints { get; set; } = 0;
    public List<AchievementDto> NewAchievements { get; set; } = new();
}
