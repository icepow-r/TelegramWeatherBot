using Newtonsoft.Json;
using System.Globalization;
using System.Net;
using System.Text;
using TelegramWeatherBot;

const string telegramToken = "token";
const string weatherKey = "key";

var client = new HttpClient();
var offset = 0;

while (true)
{
    var jsonResponse = client.GetAsync($"https://api.telegram.org/bot{telegramToken}/getUpdates?offset={offset}").Result;
    var tgUpdates = JsonConvert.DeserializeObject<UpdatesResponse>(jsonResponse.Content.ReadAsStringAsync().Result);

    if (jsonResponse.IsSuccessStatusCode && tgUpdates != null)
    {
        foreach (var update in tgUpdates.Result)
        {
            var messageText = update.Message.Text.ToLower();
            string responseMessage;

            if (messageText == "/start")
            {
                responseMessage = "Этот бот показывает погоду для любой точки мира! Чтобы узнать погоду, введите название города";
            }
            else if (messageText == "/weather")
            {
                responseMessage = "Чтобы узнать погоду, введите название города";
            }
            else
            {
                if (messageText.StartsWith("/weather"))
                {
                    messageText = messageText[8..].Trim();
                }
                jsonResponse = client.GetAsync($"http://api.openweathermap.org/data/2.5/weather?q={messageText}&APPID={weatherKey}&units=metric&lang=ru").Result;

                if (!jsonResponse.IsSuccessStatusCode)
                {
                    switch (jsonResponse.StatusCode)
                    {
                        case HttpStatusCode.NotFound:
                            responseMessage = $"Город {messageText} не найден";
                            break;
                        case HttpStatusCode.Unauthorized:
                            responseMessage = "Сервис перегружен, попробуйте снова через несколько минут. " +
                                      "Если проблема не решена, свяжитесь с разработчиком @ice_z";
                            Console.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": Превышено количество запросов в минуту или устарел API ключ погоды");
                            break;
                        default:
                            responseMessage = "Неизвестная ошибка, свяжитесь с разработчиком @ice_z";
                            Console.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + "Неизвестная ошибка!");
                            break;
                    }
                }
                else
                {
                    var weather = JsonConvert.DeserializeObject<NowWeatherModel>(jsonResponse.Content.ReadAsStringAsync().Result);
                    responseMessage = BuildMessage(weather!);
                }
            }
            await client.GetAsync($"https://api.telegram.org" +
                            $"/bot{telegramToken}" +
                            $"/sendMessage" +
                            $"?chat_id={update.Message.Chat.Id}&text={responseMessage}");
        }
        if (tgUpdates.Result.Length != 0)
        {
            offset = tgUpdates.Result[^1].UpdateId + 1;
        }
    }
    else
    {
        Console.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": Ошибка связи с Телеграм");
    }

    Thread.Sleep(2000);
}

static string BuildMessage(NowWeatherModel weather)
{
    var message = new StringBuilder();
    var windDirection = weather.Wind.Deg switch
    {
        >= 0 and < 15 or >= 345 and < 360 => "с",
        >= 15 and < 75 => "северо-восток",
        >= 75 and < 105 => "восток",
        >= 105 and < 165 => "юго-восток",
        >= 165 and < 195 => "юг",
        >= 195 and < 255 => "юго-запад",
        >= 255 and < 285 => "запад",
        >= 285 and < 345 => "северо-запад",
        _ => string.Empty
    };

    message.AppendLine("Показывается погода для города: " + weather.Name);
    message.AppendLine($"Температура: {Math.Round(weather.Main.Temp):+#;-#;+0}°C, {weather.Weather[0].Description}");
    message.AppendLine($"Ощущается как: {Math.Round(weather.Main.FeelsLike):+#;-#;+0}°C");
    message.AppendLine($"Скорость ветра: {weather.Wind.Speed:N1} м/с, направление: {windDirection}");
    message.AppendLine($"Влажность: {weather.Main.Humidity}%, давление {weather.Main.Pressure} мм рт. ст.");

    return message.ToString();
}