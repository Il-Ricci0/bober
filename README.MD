# 🦫 Bober

**“Dam problem detected, Bober is on it.”**  

Bober is a software that:  
- 🪚 Chews through server errors  
- 🪵 Stacks logs like a proper beaver  
- 🏗️ Builds dams to stop downtime floods  
- 📄 Drafts blueprints (incident reports) for every fix  

Because building a dam is good, but remembering how to build it is **even better**. 💪

<img src="assets/this-is-fine.jpg" width="400">

# 🔄 Workflow

## 1. Event Detection
- The monitoring service detects downtime or errors.  
- A webhook is triggered, sending data about the event.  
- A unique event ID is generated to track all actions related to this incident.  

## 2. Incident Report Creation (Agent 1)
- A markdown incident report is automatically created using the downtime event information.  
- The system retrieves the correct SSH credentials from a credentials pool.  
  - If credentials are unavailable, they are requested from an API.  
- Agent 1 connects to the affected server and investigates the issue.  
  - All executed commands are logged in the report.  
  - Comments are added for any command that reveals useful information about the problem.  

## 3. Problem Resolution (Optional, Agent 2)
- If the user opts for automated resolution (or it’s specified in the webhook), Agent 2 takes over.  
- Agent 2 reads the incident report and connects to the server.  
- Steps to fix the problem are executed:  
  - All commands are logged in the report.  
  - Each command includes a comment explaining the reason.  
  - Commands that could affect sensitive data or operations require user confirmation.  
- This continues until:  
  - The issue is resolved **or**  
  - A new uptime event is detected by the monitoring service.  

## 4. Outcome
- A complete, detailed incident response report is generated.  
- The affected service is restored if resolution was performed.  
- Users can choose to:  
  - **Only generate the report** (Agent 1)  
  - **Generate the report and resolve the issue** (Agent 1 + Agent 2)  