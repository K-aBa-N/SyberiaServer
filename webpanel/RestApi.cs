﻿using Nancy;
using Nancy.Helpers;
using Nancy.Responses;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.IO;
using System.Text;

namespace SyberiaWebPanel
{
    public class RestApi : NancyModule
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public RestApi()
        {
            Get("/", x =>
            {
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "login.html");
                return new GenericFileResponse(htmlPath, this.Context).NoCache();
            });

            Post("/login", x =>
            {
                var request = this.Context.Request;
                if (!WebPanel.GetInstance().CheckSpamFilter(request.UserHostAddress))
                {
                    logger.Warn($"[/login] Blocked by spam-filter. Too many failed login attempts from {request.UserHostAddress}");
                    return new Response()
                    {
                        StatusCode = HttpStatusCode.Forbidden
                    };
                }

                var body = new StreamReader(request.Body).ReadToEnd();
                var data = HttpUtility.ParseQueryString(body);
                var login = data.Get("login");
                var passMd5 = data.Get("pass");

                if (!WebPanel.GetInstance().CheckCredentials(login, passMd5))
                {
                    logger.Warn($"[/login] Invalid login from {request.UserHostAddress}. Spam filter incremented.");
                    WebPanel.GetInstance().IncrementSpamFilter(request.UserHostAddress);
                    return new Response()
                    {
                        StatusCode = HttpStatusCode.InternalServerError
                    };
                }

                WebPanel.GetInstance().ClearSpamFilter(request.UserHostAddress);

                var startTime = DateTime.Now;
                var gameConfig = WebPanel.GetInstance().GetGameConfig();
                var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "main.html");
                var htmlData = new StringBuilder(File.ReadAllText(htmlPath));
                htmlData.Replace("${m_mainConfig.m_startSoulsCount}", gameConfig.m_mainConfig.m_startSoulsCount);
                htmlData.Replace("${m_mainConfig.m_respawnSoulsPrice}", gameConfig.m_mainConfig.m_respawnSoulsPrice);
                htmlData.Replace("${m_mainConfig.m_newchar_points}", gameConfig.m_mainConfig.m_newchar_points);
                htmlData.Replace("${m_mainConfig.m_roleplay_mode}", gameConfig.m_mainConfig.m_roleplay_mode == 1);
                htmlData.Replace("${m_mainConfig.m_startMedicKit}", gameConfig.m_mainConfig.m_startMedicKit);
                htmlData.Replace("${m_mainConfig.m_startStealthKit}", gameConfig.m_mainConfig.m_startStealthKit);
                htmlData.Replace("${m_mainConfig.m_startSurvivorKit}", gameConfig.m_mainConfig.m_startSurvivorKit);
                for (int i = 0; i < gameConfig.m_mainConfig.m_skillModifiers.Length; i++)
                {
                    var skill = gameConfig.m_mainConfig.m_skillModifiers[i];
                    htmlData.Replace($"${{m_mainConfig.m_skillModifiers[{skill.m_id}].m_mod}}", skill.m_mod);
                    htmlData.Replace($"${{m_mainConfig.m_skillModifiers[{skill.m_id}].m_decreaseOnDeath}}", skill.m_decreaseOnDeath);
                }

                var timeDiff = DateTime.Now - startTime;
                logger.Info($"[/login] Successfully logged in from {request.UserHostAddress}. UI rendered in {(int)timeDiff.TotalMilliseconds} ms.");
                return new TextResponse(htmlData.ToString(), "text/html", Encoding.UTF8).NoCache();
            });

            Post("/save", x => {
                var request = this.Context.Request;
                if (!WebPanel.GetInstance().CheckSpamFilter(request.UserHostAddress))
                {
                    logger.Warn($"[/login] Blocked by spam-filter. Too many failed login attempts from {request.UserHostAddress}");
                    return new Response()
                    {
                        StatusCode = HttpStatusCode.Forbidden
                    };
                }

                var bodyString = new StreamReader(request.Body).ReadToEnd();
                var bodyObject = Newtonsoft.Json.JsonConvert.DeserializeObject(bodyString) as JToken;
                var auth = bodyObject["auth"];
                var login = auth["login"].Value<string>();
                var passMd5 = auth["pass"].Value<string>();

                if (!WebPanel.GetInstance().CheckCredentials(login, passMd5))
                {
                    logger.Warn($"[/login] Invalid login from {request.UserHostAddress}. Spam filter incremented.");
                    WebPanel.GetInstance().IncrementSpamFilter(request.UserHostAddress);
                    return new Response()
                    {
                        StatusCode = HttpStatusCode.InternalServerError
                    };
                }

                var data = bodyObject["data"];
                WebPanel.GetInstance().GetGameConfig().Apply(data);
                //WebPanel.GetInstance().GetGameConfig().Save();

                return new Response()
                {
                    StatusCode = HttpStatusCode.OK
                };
            });
        }
    }
}