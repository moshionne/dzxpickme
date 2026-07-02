using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("ApiClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7028");
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapGet("/", async (HttpContext context) =>
{
    bool isLoggedIn = context.User.Identity?.IsAuthenticated == true;
    string userName = context.User.Identity?.Name ?? "";

    string welcomeMessage = isLoggedIn ? $"Добро пожаловать, {userName}!" : "Добро пожаловать!";

    string buttonsHtml = "";
    if (isLoggedIn)
    {
        buttonsHtml = @"
            <form method='get' action='/quizzes'>
                <button type='submit' class='btn shop-btn'>Викторинки</button>
            </form>
            <form method='get' action='/create-quiz'>
                <button type='submit' class='btn shop-btn'>Создать викторину</button>
            </form>
            <form method='get' action='/leaderboard'>
                <button type='submit' class='btn shop-btn'>Список лидеров</button>
            </form>
            <form method='get' action='/profile'>
                <button type='submit' class='btn shop-btn'>Личный кабинет</button>
            </form>
            <form method='post' action='/logout'>
                <button type='submit' class='btn logout-btn'>Выйти</button>
            </form>";
    }
    else
    {
        buttonsHtml = @"
            <form method='get' action='/register'>
                <button type='submit' class='btn btn_signin'>Зарегистрироваться</button>
            </form>
            <form method='get' action='/login'>
                <button type='submit' class='btn btn_login'>Войти</button>
            </form>";
    }

    var replacements = new Dictionary<string, string>
    {
        ["welcomeMessage"] = welcomeMessage,
        ["buttonsHtml"] = buttonsHtml
    };

    var html = LoadHtml("index.html", replacements);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/api/auth/login", async (LoginRequest request, IHttpClientFactory factory, HttpContext context) =>
{
    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsJsonAsync("/api/auth/login", request);
    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

    if (result?.Success == true)
    {

        context.Response.Cookies.Append("AuthToken", result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });

        context.Response.Cookies.Append("UserId", result.UserId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            Path = "/"
        });

        var savedToken = context.Request.Cookies["AuthToken"];
        Console.WriteLine($"Токен в куках после входа: {(savedToken != null ? "ЕСТЬ" : "НЕТ")}");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, result.Login),
            new Claim("UserId", result.UserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Results.Ok(new { success = true });
    }

    return Results.BadRequest(new { success = false, message = result?.Message });
});

app.MapGet("/login", () =>
{
    var html = LoadHtml("login.html");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/logout", async (HttpContext context) =>
{
    context.Response.Cookies.Delete("AuthToken");
    context.Response.Cookies.Delete("UserId");
    context.Session.Clear();
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});
app.MapPost("/api/auth/register", async (RegisterRequest request, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("ApiClient");
    var response = await client.PostAsJsonAsync("/api/auth/register", request);
    var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
    return Results.Json(result);
});
app.MapGet("/register", () =>
{
    var html = LoadHtml("register.html");
    return Results.Content(html, "text/html; charset=utf-8");
});
app.MapGet("/quizzes", async (IHttpClientFactory factory, HttpContext context) =>
{
    var client = factory.CreateClient("ApiClient");
    var allQuizzes = await client.GetFromJsonAsync<List<QuizDto>>("/api/quizzes");

    string userId = context.User.FindFirstValue("UserId");
    int adminId = 1;

    var defquizzes = allQuizzes?.Where(q => q.CreatorId == adminId).ToList() ?? new List<QuizDto>();
    var customquizzes = allQuizzes?.Where(q => q.CreatorId != adminId).ToList() ?? new List<QuizDto>();

    string defquizzesHtml = "";
    foreach (var quiz in defquizzes)
    {
        bool isCreator = !string.IsNullOrEmpty(userId) && int.Parse(userId) == 1;
        defquizzesHtml += $@"
        <div class='quiz-card'>
            <div class='quiz-badge official'>Официальная</div>
            <h3>{quiz.Title}</h3>
            <p>{quiz.Description ?? "Описание отсутствует"}</p>
            <div class='quiz-stats'>
                <span>★ {quiz.QuestionsCount} вопросов</span>
                <span>★ {quiz.TotalPoints} баллов</span>
                <span>★ {quiz.TimesPlayed} игр</span>
            </div>
            <div class='card-buttons'>
                <a href='/quiz/{quiz.Id}' class='btn-play'>Пройти</a>";
        if (isCreator)
        {
            defquizzesHtml += $@"
                <form method='post' action='/delete-quiz/{quiz.Id}' style='display:inline;' onsubmit='return confirm(""Удалить викторину?"");'>
                    <button type='submit' class='btn-delete'>Удалить</button>
                </form>";
        }
        defquizzesHtml += @"
            </div>
        </div>";
    }

    string customquizzesHtml = "";
    foreach (var quiz in customquizzes)
    {
        bool isCreator = !string.IsNullOrEmpty(userId) && (int.Parse(userId) == quiz.CreatorId || int.Parse(userId) == 1);

        customquizzesHtml += $@"
        <div class='quiz-card'>
            <div class='quiz-badge user'>Пользовательская</div>
            <h3>{quiz.Title}</h3>
            <p>{quiz.Description ?? "Описание отсутствует"}</p>
            <div class='quiz-stats'>
                <span>★ {quiz.QuestionsCount} вопросов</span>
                <span>★ {quiz.TotalPoints} баллов</span>
                <span>★ {quiz.TimesPlayed} игр</span>
            </div>
            <div class='quiz-creator'>Автор: {quiz.CreatorName}</div>
            <div class='card-buttons'>
                <a href='/quiz/{quiz.Id}' class='btn-play'>Пройти</a>";

        if (isCreator)
        {
            customquizzesHtml += $@"
                <form method='post' action='/delete-quiz/{quiz.Id}' style='display:inline;' onsubmit='return confirm(""Удалить викторину?"");'>
                    <button type='submit' class='btn-delete'>Удалить</button>
                </form>";
        }

        customquizzesHtml += @"
            </div>
        </div>";
    }

    if (!customquizzes.Any())
    {
        customquizzesHtml = "<div class='empty-state'>Пока нет пользовательских викторин<br><a href='/create-quiz'>Создать первую</a></div>";
    }

    string createCardHtml = @"
    <div class='quiz-card-wrapper'>
        <a href='/create-quiz' class='create-card-link'>
            <div class='quiz-card create-card'>
                <div class='quiz-badge create'>Создать</div>
                <h3>⋆˚🐾˖°</h3>
                <p>Создайте свою собственную викторину</p>
                <div class='quiz-stats'>
                    <span>★ Добавьте вопросы</span>
                    <span>★ Назначьте баллы</span>
                </div>
                <div class='quiz-creator'>Станьте автором!</div>
            </div>
        </a>
    </div>";

    var replacements = new Dictionary<string, string>
    {
        ["defquizzesHtml"] = defquizzesHtml,
        ["customquizzesHtml"] = customquizzesHtml + createCardHtml
    };

    var html = LoadHtml("quizzes.html", replacements);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/quiz/{id:int}", async (int id, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("ApiClient");
    var quiz = await client.GetFromJsonAsync<QuizDetailDto>($"/api/quizzes/{id}");

    if (quiz == null) return Results.NotFound("Викторина не найдена");

    string questionsHtml = "";
    int qNum = 1;
    foreach (var question in quiz.Questions)
    {
        string answersHtml = "";
        foreach (var answer in question.Answers)
        {
            answersHtml += $@"
            <label class='answer-option'>
                <input type='radio' name='q{question.Id}' value='{answer.Id}' required>
                <span>{answer.Text}</span>
            </label>";
        }

        string escapedQuestionText = question.Text.Replace("\\", "\\\\").Replace("`", "\\`").Replace("'", "\\'");

        questionsHtml += $@"
        <div class='question-block' id='question-block-{question.Id}'>
            <div class='question-header'>
                <span class='question-num'>Вопрос {qNum}</span>
                <span class='question-points'>★ {question.Points} очков</span>
            </div>
            <div class='question-text'>
                <span id='question-text-{question.Id}'>{question.Text}</span>
                <button type='button' class='hint-btn' data-id='{question.Id}' data-text='{escapedQuestionText}'>Подсказка</button>
            </div>
            <div id='hint-{question.Id}' class='hint-box' style='display: none;'></div>
            <div class='answers'>
                {answersHtml}
            </div>
        </div>";
        qNum++;
    }

    var replacements = new Dictionary<string, string>
    {
        ["Title"] = quiz.Title,
        ["Description"] = quiz.Description ?? "Нет описания",
        ["TotalPoints"] = quiz.TotalPoints.ToString(),
        ["Id"] = quiz.Id.ToString(),
        ["QuestionsHtml"] = questionsHtml
    };

    var html = LoadHtml("quiz.html", replacements);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/quiz/{id:int}/submit", async (int id, HttpContext context, IHttpClientFactory factory) =>
{
    var request = await context.Request.ReadFromJsonAsync<SubmitQuizRequest>();
    if (request == null)
    {
        Console.WriteLine("❌ Не удалось прочитать JSON");
        return Results.BadRequest("Invalid request");
    }

    var client = factory.CreateClient("ApiClient");

    var token = context.Request.Cookies["AuthToken"];
    if (string.IsNullOrEmpty(token)) return Results.Redirect("/login");

    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var data = new
    {
        Answers = request.Answers,
        UsedHints = request.UsedHints,
        TotalQuestions = request.TotalQuestions
    };

    Console.WriteLine($"📤 Отправка в Веб-API: {System.Text.Json.JsonSerializer.Serialize(data)}");

    var response = await client.PostAsJsonAsync($"/api/quizzes/{id}/submit", data);

    var responseContent = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"📥 Ответ от Веб-API: {responseContent}");

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"❌ Ошибка от Веб-API: {response.StatusCode}, {responseContent}");
        return Results.Redirect($"/quiz/{id}?error=api");
    }

    var result = await response.Content.ReadFromJsonAsync<SubmitResultDto>();

    string achievementsHtml = "";
    string toastScript = "";

    if (result?.Gamification?.NewAchievements != null && result.Gamification.NewAchievements.Count > 0)
    {
        foreach (var ach in result.Gamification.NewAchievements)
        {
            achievementsHtml += $@"
            <div class='toast-notification'>
                <div class='toast-icon'>{ach.Icon}</div>
                <div>
                    <div class='toast-title'>🎉 Достижение разблокировано!</div>
                    <div class='toast-name'>{ach.Name}</div>
                    <div class='toast-desc'>{ach.Description}</div>
                    <span class='toast-xp'>+{ach.XP} XP</span>
                </div>
                <button class='toast-close' onclick='this.parentElement.remove()'>✕</button>
            </div>";
        }

        toastScript = @"
        <script>
            document.querySelectorAll('.toast-notification').forEach(toast => {
                setTimeout(() => { toast.classList.add('toast-hiding'); setTimeout(() => toast.remove(), 500); }, 5000);
                toast.addEventListener('click', () => { toast.classList.add('toast-hiding'); setTimeout(() => toast.remove(), 500); });
            });
        </script>";
    }

    var replacements = new Dictionary<string, string>
    {
        ["Score"] = result?.Score.ToString() ?? "0",
        ["MaxScore"] = result?.MaxScore.ToString() ?? "0",
        ["Percentage"] = result?.Percentage.ToString() ?? "0",
        ["Level"] = result?.Gamification?.Level.ToString() ?? "1",
        ["Streak"] = result?.Gamification?.Streak.ToString() ?? "0",
        ["League"] = result?.Gamification?.League ?? "Бронза",
        ["AchievementsHtml"] = achievementsHtml,
        ["ToastScript"] = toastScript
    };

    var html = LoadHtml("result.html", replacements);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/gamification", async (HttpContext context, IHttpClientFactory factory) =>
{
    var token = context.Request.Cookies["AuthToken"];
    if (string.IsNullOrEmpty(token)) return Results.Redirect("/login");

    var client = factory.CreateClient("ApiClient");
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync("/api/gamification");
    var result = await response.Content.ReadFromJsonAsync<GamificationDto>();
    return Results.Ok(result);
});

app.MapPost("/delete-quiz/{id}", async (int id, IHttpClientFactory factory, HttpContext context) =>
{
    var client = factory.CreateClient("ApiClient");
    var token = context.Request.Cookies["AuthToken"];

    if (string.IsNullOrEmpty(token)) return Results.Redirect("/login");

    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var response = await client.DeleteAsync($"/api/quizzes/{id}");
    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return Results.Redirect("/login");

    return Results.Redirect("/quizzes?deleted=true");
});


app.MapGet("/leaderboard", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("ApiClient");
    var leaderboard = await client.GetFromJsonAsync<List<LeaderboardItem>>("/api/leaderboard?sort=totalscore");

    string rows = "";
    foreach (var item in leaderboard ?? new List<LeaderboardItem>())
    {
        rows += $@"
        <tr>
            <td>{item.Rank}</td>
            <td>{item.Login}</td>
            <td>{item.TotalPoints}</td>
            <td>{item.QuizzesPassed}</td>
            <td>{item.QuizzesCreated}</td>
        </tr>";
    }

    var replacements = new Dictionary<string, string>
    {
        ["Rows"] = rows
    };

    var html = LoadHtml("leaderboard.html", replacements);
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/create-quiz", (HttpContext context) =>
{
    var html = LoadHtml("create-quiz.html");
    return Results.Content(html, "text/html; charset=utf-8");
});
app.MapPost("/create-quiz", async (HttpContext context, IHttpClientFactory factory) =>
{
    string userId = context.User.FindFirstValue("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Redirect("/login");
    }

    var form = await context.Request.ReadFormAsync();

    string title = form["title"].ToString();
    string description = form["description"].ToString();
    string difficultyStr = form["difficulty"].ToString();

    if (string.IsNullOrEmpty(title))
    {
        return Results.Redirect("/create-quiz?error=empty_title");
    }

    int difficulty = 1;
    if (!string.IsNullOrEmpty(difficultyStr))
    {
        int.TryParse(difficultyStr, out difficulty);
    }

    var questions = new List<QuestionRequestDto>();
    var questionIndices = new HashSet<int>();

    foreach (var key in form.Keys)
    {
        string keyStr = key.ToString();
        if (keyStr.Contains("questions[") && keyStr.EndsWith("].text"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(keyStr, @"questions\[(\d+)\]\.text");
            if (match.Success)
            {
                questionIndices.Add(int.Parse(match.Groups[1].Value));
            }
        }
    }
    foreach (int questionIndex in questionIndices.OrderBy(i => i))
    {
        string questionText = form[$"questions[{questionIndex}].text"].ToString();
        string pointsKey = $"questions[{questionIndex}].points";
        string pointsStr = form[pointsKey].ToString();

        int points = 10;
        if (!string.IsNullOrEmpty(pointsStr))
        {
            int.TryParse(pointsStr, out points);
        }

        var answers = new List<AnswerRequestDto>();
        var answerIndices = new HashSet<int>();

        foreach (var key in form.Keys)
        {
            string keyStr = key.ToString();
            if (keyStr.Contains($"questions[{questionIndex}].answers[") && keyStr.EndsWith("].text"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(keyStr, @"answers\[(\d+)\]\.text");
                if (match.Success)
                {
                    answerIndices.Add(int.Parse(match.Groups[1].Value));
                }
            }
        }

        foreach (int answerIndex in answerIndices.OrderBy(i => i))
        {
            string answerText = form[$"questions[{questionIndex}].answers[{answerIndex}].text"].ToString();
            string isCorrectKey = $"questions[{questionIndex}].answers[{answerIndex}].isCorrect";
            bool isCorrect = form.ContainsKey(isCorrectKey);

            if (!string.IsNullOrEmpty(answerText))
            {
                answers.Add(new AnswerRequestDto
                {
                    Text = answerText,
                    IsCorrect = isCorrect
                });
            }
        }

        if (!string.IsNullOrEmpty(questionText))
        {
            questions.Add(new QuestionRequestDto
            {
                Text = questionText,
                Points = points,
                Answers = answers
            });
        }
    }

    if (questions.Count == 0)
    {
        return Results.Redirect("/create-quiz?error=no_questions");
    }

    var request = new CreateQuizRequestDto
    {
        Title = title,
        Description = description ?? "",
        Difficulty = difficulty,
        Questions = questions
    };


    var token = context.Request.Cookies["AuthToken"];

    var client = factory.CreateClient("ApiClient");
    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    try
    {
        var response = await client.PostAsJsonAsync("/api/quizzes", request);
        var responseBody = await response.Content.ReadAsStringAsync();

        var result = System.Text.Json.JsonSerializer.Deserialize<CreateQuizResponse>(responseBody);

        if (result?.Id > 0)
        {
            return Results.Redirect($"/quizzes");
        }
        else
        {
            return Results.Redirect("/create-quiz?error=api_error");
        }
    }
    catch (Exception ex)
    {
        return Results.Redirect("/create-quiz?error=true");
    }
});
app.MapPost("/api/gigachat/generate-quiz", async Task<IResult> (GenerateQuizRequest request, IHttpClientFactory factory, HttpContext context) =>
{
    var client = factory.CreateClient("ApiClient");

    var token = context.Request.Cookies["AuthToken"];
    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    var response = await client.PostAsJsonAsync("/api/gigachat/generate-quiz", request);
    var result = await response.Content.ReadFromJsonAsync<GenerateQuizResponse>();

    return Results.Ok(result);
});

app.MapPost("/api/gigachat/hint", async (GigaChatRequest request, IHttpClientFactory factory, HttpContext context) =>
{
    var client = factory.CreateClient("ApiClient");

    var token = context.Request.Cookies["AuthToken"];
    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    var response = await client.PostAsJsonAsync("/api/gigachat/hint", request);
    var result = await response.Content.ReadFromJsonAsync<GigaChatResponse>();

    return Results.Ok(result);
});
app.MapPost("/api/quizzes", async (HttpContext context, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("ApiClient");
    var token = context.Request.Cookies["AuthToken"];

    if (string.IsNullOrEmpty(token)) return Results.Unauthorized();

    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    var request = await context.Request.ReadFromJsonAsync<CreateQuizRequest>();
    var response = await client.PostAsJsonAsync("/api/quizzes", request);
    var result = await response.Content.ReadFromJsonAsync<CreateQuizResponse>();

    return Results.Ok(result);
});
app.MapGet("/profile", async (HttpContext context, IHttpClientFactory factory) =>
{
    var token = context.Request.Cookies["AuthToken"];
    if (string.IsNullOrEmpty(token)) return Results.Redirect("/login");

    var client = factory.CreateClient("ApiClient");
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    try
    {
        var profile = await client.GetFromJsonAsync<ProfileDto>("/api/profile");
        if (profile == null) return Results.Redirect("/login");
        var gamification = await client.GetFromJsonAsync<GamificationDto>("/api/gamification");
        string achievementsHtml = "";
        if (gamification?.Achievements != null && gamification.Achievements.Count > 0)
        {
            foreach (var ach in gamification.Achievements)
            {
                achievementsHtml += $"<span class='achievement'>{ach.Icon} {ach.Name} (+{ach.XP} XP)</span>";
            }
        }
        else
        {
            achievementsHtml = "<span style='color:#999;'>Пройдите викторины, чтобы открыть достижения!</span>";
        }
        var replacements = new Dictionary<string, string>
        {
            ["Login"] = profile.Login,
            ["TotalPoints"] = profile.TotalPoints.ToString(),
            ["XP"] = gamification?.XP.ToString() ?? "0",
            ["Level"] = gamification?.Level.ToString() ?? "1",
            ["Streak"] = gamification?.Streak.ToString() ?? "0",
            ["League"] = gamification?.League ?? "Бронза",
            ["QuizzesPassed"] = profile.QuizzesPassed.ToString(),
            ["QuizzesCreated"] = profile.QuizzesCreated.ToString(),
            ["BestScore"] = profile.BestScore.ToString(),
            ["AchievementsHtml"] = achievementsHtml
        };

        var html = LoadHtml("profile.html", replacements);
        return Results.Content(html, "text/html; charset=utf-8");
    }
    catch (Exception ex)
    {
        return Results.Redirect("/login");
    }
});

app.Run();
static string LoadHtml(string fileName, Dictionary<string, string>? replacements = null)
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pages", fileName);
    var content = File.ReadAllText(path);

    if (replacements != null)
    {
        foreach (var kvp in replacements)
        {
            content = content.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }
    }

    return content;
}
public class GigaChatRequest { 
    public string QuestionText { get; set; } = "";
}

public class GigaChatResponse { 
    public string Hint { get; set; } = "";
}
public class LoginRequest { 
    public string Login { get; set; } = "";
    public string Password { get; set; } = ""; 
}
public class LoginResponse { 
    public bool Success { get; set; }
    public string Message { get; set; } = ""; 
    public int UserId { get; set; }
    public string Login { get; set; } = "";
    public string Token { get; set; } = ""; 
}
public class RegisterRequest { 
    public string Login { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}
public class RegisterResponse 
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
public class ProfileDto 
{
    public int Id { get; set; } public string Login { get; set; } = "";
    public int TotalPoints { get; set; }
    public int QuizzesPassed { get; set; } 
    public int QuizzesCreated { get; set; } public int BestScore { get; set; } 
}
public class QuizDto { 
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; } 
    public int QuestionsCount { get; set; }
    public int TotalPoints { get; set; } 
    public int TimesPlayed { get; set; }
    public int CreatorId { get; set; }
    public string CreatorName { get; set; } = ""; }
public class QuizDetailDto { 
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int TotalPoints { get; set; }
    public List<QuestionDetailDto> Questions { get; set; } = new(); }
public class QuestionDetailDto { 
    public int Id { get; set; } public string Text { get; set; } = ""; 
    public int Points { get; set; } 
    public List<AnswerDetailDto> Answers { get; set; } = new(); 
}
public class AnswerDetailDto { 
    public int Id { get; set; }
    public string Text { get; set; } = ""; 
    public bool IsCorrect { get; set; } 
}
public class UserAnswerDto
{
    public int QuestionId { get; set; }
    public int AnswerId { get; set; }
}
public class SubmitResultDto { 
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public int Percentage { get; set; }
    public GamificationData Gamification { get; set; } = new();
}
public class LeaderboardItem {
    public int Rank { get; set; }
    public string Login { get; set; } = ""; 
    public int TotalPoints { get; set; }
    public int QuizzesPassed { get; set; }
    public int QuizzesCreated { get; set; } 
}
public class GamificationData
{
    public int XP { get; set; }
    public int Level { get; set; }
    public int Streak { get; set; }
    public string League { get; set; } = "";
    public List<AchievementDto> NewAchievements { get; set; } = new();
}
public class CreateQuizRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Difficulty { get; set; }
    public List<QuestionRequestDto> Questions { get; set; } = new();
}
public class CreateQuizRequestDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Difficulty { get; set; }
    public List<QuestionRequestDto> Questions { get; set; } = new();
}
public class QuestionRequestDto
{
    public string Text { get; set; } = "";
    public int Points { get; set; }
    public List<AnswerRequestDto> Answers { get; set; } = new();
}
public class AnswerRequestDto
{
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}
public class CreateQuizResponse
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
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
public class GamificationDto
{
    public int XP { get; set; }
    public int Level { get; set; }
    public int Streak { get; set; }
    public string League { get; set; } = "";
    public List<AchievementDto> Achievements { get; set; } = new();
}

public class AchievementDto
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public int XP { get; set; }
    public DateTime UnlockedAt { get; set; }
}
public class SubmitQuizRequest
{
    public List<UserAnswerDto> Answers { get; set; } = new();
    public int UsedHints { get; set; } = 0;
    public int TotalQuestions { get; set; } = 0;
}

