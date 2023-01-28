using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using TelegramWeatherBot;

const string telegramToken = "token";
const string weatherKey = "key";
const string offsetFile = "offset.txt";

if (!File.Exists(offsetFile))
{
    File.Create(offsetFile).Close();
    File.WriteAllText(offsetFile, "0");
}

var client = new HttpClient();
var offset = int.Parse(File.ReadAllText(offsetFile));

while (true)
{
    var jsonResponse = client.GetAsync($"https://api.telegram.org/bot{telegramToken}/getUpdates").Result;
    var tgUpdates = JsonConvert.DeserializeObject<UpdatesResponse>(jsonResponse.Content.ReadAsStringAsync().Result);

    if (!jsonResponse.IsSuccessStatusCode || tgUpdates == null)
    {
        Console.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": Telegram error");
    }
    else
    {
        for (var i = offset; i < tgUpdates.Result.Length; i++)
        {
            var update = tgUpdates.Result[i];
            var city = update.Message.Text;
            jsonResponse = client.GetAsync($"http://api.openweathermap.org/data/2.5/weather?q={city}&APPID={weatherKey}&units=metric&lang=ru").Result;
            var weather = JsonConvert.DeserializeObject<NowWeatherModel>(jsonResponse.Content.ReadAsStringAsync().Result);

            if (!jsonResponse.IsSuccessStatusCode || weather == null)
            {
                Console.WriteLine(DateTime.Now.ToString(CultureInfo.InvariantCulture) + ": Weather error");
            }
            else
            {
                await client.GetAsync($"https://api.telegram.org" +
                                $"/bot{telegramToken}" +
                                $"/sendMessage" +
                                $"?chat_id={update.Message.Chat.Id}&text={BuildMessage(weather)}");
            }
        }

        if (offset != tgUpdates.Result.Length)
        {
            offset = tgUpdates.Result.Length;
            File.WriteAllText(offsetFile, offset.ToString());
        }

    }
    Thread.Sleep(2000);
}

static string BuildMessage(NowWeatherModel weather)
{
    var message = new StringBuilder();

    var windDirection = weather.Wind.Deg switch
    {
        >= 0 and < 15 or >= 345 and < 360 => "с",
        >= 15 and < 75 => "св",
        >= 75 and < 105 => "в",
        >= 105 and < 165 => "юв",
        >= 165 and < 195 => "ю",
        >= 195 and < 255 => "юз",
        >= 255 and < 285 => "з",
        >= 285 and < 345 => "сз",
        _ => string.Empty
    };

    message.AppendLine("Показывается погода для города: " + weather.Name);
    message.AppendLine($"Температура: {Math.Round(weather.Main.Temp):+#;-#;+0}°C, {weather.Weather[0].Description}");
    message.AppendLine($"Ощущается как: {Math.Round(weather.Main.FeelsLike):+#;-#;+0}°C");
    message.AppendLine($"Скорость ветра: {weather.Wind.Speed:N1} м/с, направление: {windDirection}");
    message.AppendLine($"Влажность: {weather.Main.Humidity}%, давление {weather.Main.Pressure} мм рт. ст.");

    return message.ToString();
}