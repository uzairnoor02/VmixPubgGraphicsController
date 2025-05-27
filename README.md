# PUBG Tournament Ranking and vMix Graphics System
This project is a comprehensive system for managing PUBG tournaments, tracking player and team statistics, and integrating with vMix for live graphics display.

## Project Structure
- **Pubg Ranking System/**: A Windows Forms application for managing tournaments, teams, players, and statistics.
- **Vmix Hangfire Graphics/**: Handles background tasks using Hangfire, likely for updating vMix graphics.
- **VmixData/**: Contains data models and database context for the application.
- **VmixGraphicsBusiness/**: Includes business logic for graphics generation, statistics processing, and data management.
- **VmixPubgGraphicsController/**: An API controller, likely for communication with vMix or other external services.

## Core Functionalities
- **Tournament Management**: Allows users to create and manage PUBG tournaments, including details like stages, matches, and participating teams.
- **Player and Team Statistics**: Tracks and stores detailed statistics for players and teams, such as kills, assists, damage, and rankings.
- **vMix Integration**: Dynamically updates graphics in vMix for live tournament broadcasts, displaying real-time information like scores, player stats, and rankings.
- **Data Management**: Utilizes a database (inferred from VmixData and .csproj files) to store and retrieve tournament, player, and match data.
- **API for Graphics Control**: Provides an API (inferred from VmixPubgGraphicsController) for controlling and updating graphics elements.

## Setup and Usage
This is a .NET-based project. To set up and run this system, you would typically need:
- A .NET development environment (Visual Studio recommended).
- Access to the database system used by the project (details would be in configuration files like `appsettings.json` or within `VmixData`).
- vMix software for the live graphics integration.

**General Steps:**
1.  Clone the repository.
2.  Open the solution file (e.g., `VmixPubgGraphicsController.sln`) in Visual Studio.
3.  Restore NuGet packages.
4.  Configure database connection strings in the relevant `appsettings.json` files within each project.
5.  Build the solution.
6.  The `Pubg Ranking System` is a Windows Forms application and can likely be run directly.
7.  The `VmixPubgGraphicsController` is an API and would be hosted (e.g., via IIS Express or Kestrel).
8.  The `Vmix Hangfire Graphics` project would also need to be running for background graphics updates.

Note: Detailed setup for vMix integration and specific database configurations would require further information from the project's original developers.

## Contributing
Contributions to this project are welcome. If you'd like to contribute, please consider the following:
- Fork the repository.
- Create a new branch for your feature or bug fix.
- Make your changes and ensure they are well-tested.
- Submit a pull request with a clear description of your changes.
