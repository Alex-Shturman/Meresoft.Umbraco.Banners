namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>The shipped list of lower case user agent substrings that identify automated clients.</summary>
internal static class BotPatterns
{
    /// <summary>Plain, case-insensitive substrings. Never regular expressions.</summary>
    public static readonly string[] Shipped =
    [
        // Search engine and other crawlers
        "googlebot", "bingbot", "yandexbot", "baiduspider", "duckduckbot", "slurp", "applebot",
        "ahrefsbot", "semrushbot", "mj12bot", "dotbot", "petalbot", "bytespider", "gptbot",
        "claudebot", "ccbot", "amazonbot", "crawler", "spider", "crawl/",

        // Generic forms, chosen so that a phone brand such as CUBOT_KINGKONG does not match
        "bot/", "bot;", "bot)", "bot-",

        // Link previewers
        "facebookexternalhit", "facebot", "twitterbot", "linkedinbot", "slackbot", "telegrambot",
        "discordbot", "whatsapp", "pinterestbot", "skypeuripreview", "embedly",

        // Uptime monitors and speed tools
        "uptimerobot", "pingdom", "statuscake", "site24x7", "nagios", "zabbix", "datadog",
        "newrelic", "gtmetrix", "lighthouse", "pagespeed", "monitor/",

        // Headless browsers and automation
        "headlesschrome", "headless", "phantomjs", "puppeteer", "selenium", "webdriver",

        // Script and command line clients
        "curl/", "wget/", "python-requests", "python-urllib", "go-http-client", "libwww-perl",
        "apache-httpclient", "node-fetch", "scrapy", "httpunit", "java/"
    ];
}
