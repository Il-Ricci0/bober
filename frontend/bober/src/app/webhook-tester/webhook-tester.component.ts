import { Component, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

interface MonitorEvent {
  Url: string;
  StatusCode: string;
}

interface WebhookResponse {
  status: string;
  incidentId: string;
  incident: {
    url: string;
    statusCode: string;
  };
  directoryPath: string;
  message: string;
  timestamp: string;
}

enum WorkflowState {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4
}

interface WorkflowStatus {
  incidentId: string;
  state: WorkflowState | number;
  currentPhase: string;
  analyzerIterations: number;
  summarizerIterations: number;
  startTime: string;
  endTime?: string;
  errorMessage?: string;
  monitorEvent: {
    url?: string;
    statusCode?: string;
    Url?: string;
    StatusCode?: string;
  };
  directoryPath: string;
}

@Component({
  selector: 'app-webhook-tester',
  imports: [CommonModule, FormsModule],
  templateUrl: './webhook-tester.component.html',
  styleUrl: './webhook-tester.component.scss'
})
export class WebhookTesterComponent implements OnInit, OnDestroy {
  url = signal('http://192.168.1.22:80/');
  statusCode = signal('404');
  apiEndpoint = signal('http://localhost:5250/webhook/incident');
  apiBase = 'http://localhost:5250';

  isLoading = signal(false);
  response = signal<WebhookResponse | null>(null);
  error = signal<string | null>(null);
  workflows = signal<WorkflowStatus[]>([]);

  private pollInterval: any;

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadWorkflows();
    // Poll for workflow updates every 2 seconds
    this.pollInterval = setInterval(() => {
      this.loadWorkflows();
    }, 2000);
  }

  ngOnDestroy(): void {
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
    }
  }

  loadWorkflows(): void {
    this.http.get<WorkflowStatus[]>(`${this.apiBase}/workflows`)
      .subscribe({
        next: (workflows) => {
          console.log('Loaded workflows:', workflows);
          this.workflows.set(workflows);
        },
        error: (err) => {
          console.error('Error loading workflows:', err);
        }
      });
  }

  sendWebhook(): void {
    this.isLoading.set(true);
    this.response.set(null);
    this.error.set(null);

    const payload: MonitorEvent = {
      Url: this.url(),
      StatusCode: this.statusCode()
    };

    this.http.post<WebhookResponse>(this.apiEndpoint(), payload)
      .subscribe({
        next: (res) => {
          this.response.set(res);
          this.isLoading.set(false);
          // Immediately refresh workflows
          this.loadWorkflows();
        },
        error: (err) => {
          this.error.set(err.message || 'An error occurred');
          this.isLoading.set(false);
        }
      });
  }

  cancelWorkflow(incidentId: string): void {
    this.http.post(`${this.apiBase}/workflows/${incidentId}/cancel`, {})
      .subscribe({
        next: () => {
          this.loadWorkflows();
        },
        error: (err) => {
          console.error('Error cancelling workflow:', err);
        }
      });
  }

  getStateLabel(state: WorkflowState | number): string {
    switch (state) {
      case WorkflowState.Pending:
      case 0:
        return 'Pending';
      case WorkflowState.Running:
      case 1:
        return 'Running';
      case WorkflowState.Completed:
      case 2:
        return 'Completed';
      case WorkflowState.Failed:
      case 3:
        return 'Failed';
      case WorkflowState.Cancelled:
      case 4:
        return 'Cancelled';
      default:
        return 'Unknown';
    }
  }

  getStateClass(state: WorkflowState | number): string {
    switch (state) {
      case WorkflowState.Running:
      case 1:
        return 'state-running';
      case WorkflowState.Completed:
      case 2:
        return 'state-completed';
      case WorkflowState.Failed:
      case 3:
        return 'state-failed';
      case WorkflowState.Cancelled:
      case 4:
        return 'state-cancelled';
      default:
        return 'state-pending';
    }
  }

  getStateBadgeClass(state: WorkflowState | number): string {
    switch (state) {
      case WorkflowState.Pending:
      case 0:
        return 'badge-pending';
      case WorkflowState.Running:
      case 1:
        return 'badge-running';
      case WorkflowState.Completed:
      case 2:
        return 'badge-completed';
      case WorkflowState.Failed:
      case 3:
        return 'badge-failed';
      case WorkflowState.Cancelled:
      case 4:
        return 'badge-cancelled';
      default:
        return 'badge-pending';
    }
  }

  canCancel(state: WorkflowState | number): boolean {
    const result = state === WorkflowState.Pending ||
                   state === WorkflowState.Running ||
                   state === 0 ||
                   state === 1;
    console.log('canCancel:', state, this.getStateLabel(state), result);
    return result;
  }

  resetForm(): void {
    this.url.set('http://192.168.1.22:80/');
    this.statusCode.set('404');
    this.response.set(null);
    this.error.set(null);
  }
}
