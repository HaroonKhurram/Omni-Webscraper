# 🌐 Omni‑Webscraper: Universal Data Extraction & Aggregation System

A polymorphic **C# .NET Core engine** with adaptive HTML/JSON parsing and **Microsoft SQL Server persistence**.  
Designed as a **Senior Capstone Project** to demonstrate enterprise‑grade web scraping, schema‑agnostic storage, and headless automation.

---

## 📖 Overview
Traditional scrapers break when websites change layouts or switch from HTML to JSON feeds.  
Omni‑Webscraper solves this by implementing a **polymorphic, parameter‑driven abstraction layer** that adapts dynamically to any web target.

- **Adaptive HTML/JSON Parsing** (HtmlAgilityPack + System.Text.Json)  
- **Dynamic SQL Storage** with schema‑agnostic key‑value design  
- **Headless Automation** for enterprise orchestration  
- **Fault Isolation Controls** for continuous background extraction  

---

## 🧩 Architecture
Omni‑Webscraper is built on four polymorphic layers:

| Layer | Purpose |
|-------|---------|
| **[Dynamic Orchestration](ca://s?q=Explain_dynamic_orchestration_layer)** | Accepts runtime parameters (URLs, selectors, flags) |
| **[Polymorphic Request Engine](ca://s?q=Explain_polymorphic_request_engine)** | Wraps HttpClient, manages headers, cookies, proxies |
| **[Adaptive Extraction Matrix](ca://s?q=Explain_adaptive_extraction_matrix)** | Decides between HTML DOM, JSON feeds, or regex |
| **[Dynamic MSSQL Persistence](ca://s?q=Explain_dynamic_MSSQL_persistence)** | Stores extracted values safely in schema‑agnostic tables |

---

## 💻 Implementation
Core C# class:  

```csharp
public async Task<string> ExtractTargetDataAsync(string targetUrl, string selector, bool isJsonFeed)
{
    string rawContent = await _client.GetStringAsync(targetUrl);
    if (isJsonFeed)
    {
        using (JsonDocument parsedJson = JsonDocument.Parse(rawContent))
        {
            if (parsedJson.RootElement.TryGetProperty(selector, out JsonElement targetedNode))
                return targetedNode.ToString();
        }
    }
    else
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(rawContent);
        var nodeResult = htmlDoc.DocumentNode.SelectSingleNode(selector);
        return nodeResult?.InnerText.Trim();
    }
    return null;
}
