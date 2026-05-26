using System.ComponentModel;
using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

/// <summary>
/// Tools available to the travel planning agents for looking up destinations, prices, and availability.
/// </summary>
public static class TravelTools
{
    [Description("Searches for restaurants and food experiences at a destination. Returns top-rated options with prices.")]
    [ExportAIFunction("search_restaurants")]
    public static string SearchRestaurants(
        [Description("The travel destination city")] string destination,
        [Description("Type of cuisine to search for (optional)")] string? cuisine = null)
    {
        var city = destination.ToLowerInvariant();
        if (city.Contains("cape town"))
            return """
                🍽️ Top Restaurants in Cape Town:
                1. The Test Kitchen - Tasting menu R1,200pp ⭐4.9 (Fine dining, Woodstock)
                2. La Colombe - 8-course R1,500pp ⭐4.8 (French-Asian fusion, Constantia)
                3. Harbour House - Mains R180-R350 ⭐4.6 (Seafood, V&A Waterfront)
                4. The Pot Luck Club - Small plates R80-R150 ⭐4.7 (Tapas, Silo District)
                5. Gold Restaurant - Set menu R650pp ⭐4.5 (Pan-African, Green Point)
                Booking recommended for all. Peak season: Dec-Feb.
                """;
        return $"""
            🍽️ Top Restaurants in {destination}:
            1. The Local Table - Prix fixe $85pp ⭐4.8 (Modern local cuisine)
            2. Market Street Kitchen - Mains $25-$45 ⭐4.7 (Farm-to-table)
            3. Skyline Rooftop - Tasting menu $120pp ⭐4.6 (Contemporary)
            Booking recommended 2-3 days in advance.
            """;
    }

    [Description("Checks transport options and approximate costs between locations at the destination.")]
    [ExportAIFunction("check_transport")]
    public static string CheckTransport(
        [Description("The travel destination city")] string destination,
        [Description("Starting point within the city")] string from,
        [Description("End point within the city")] string to)
    {
        return $"""
            🚗 Transport: {from} → {to} ({destination})
            ─────────────────────────────────────
            • Uber/Taxi: ~15-25 min, estimated $12-$18
            • Public Transit: ~35 min, $2.50 (bus/metro)
            • Walking: ~45 min (if <3km)
            • Rental Car: Available from $35/day + parking $8-$15
            
            ℹ️ Tip: {(destination.ToLowerInvariant().Contains("cape town")
                ? "MyCiti bus is reliable for Waterfront/CBD. Uber is safest for evening travel."
                : "Local rideshare apps often cheaper than international ones.")}
            """;
    }

    [Description("Looks up current pricing and availability for accommodations at a destination.")]
    [ExportAIFunction("check_accommodation")]
    public static string CheckAccommodation(
        [Description("The travel destination city")] string destination,
        [Description("Budget level: budget, mid-range, or luxury")] string budget = "mid-range")
    {
        var priceRange = budget.ToLowerInvariant() switch
        {
            "budget" => "$40-$80/night",
            "luxury" => "$250-$600/night",
            _ => "$100-$200/night"
        };
        return $"""
            🏨 Accommodation in {destination} ({budget}):
            ─────────────────────────────────────
            Price range: {priceRange}
            Availability: Good (65% occupancy this period)
            
            Recommended areas:
            • City Center - walkable, restaurants nearby
            • Waterfront/Harbor - scenic, tourist-friendly
            • Local neighborhood - authentic, quieter, cheaper
            
            ⚡ Best value: Book 2-3 weeks ahead for 15-20% savings.
            """;
    }
}
