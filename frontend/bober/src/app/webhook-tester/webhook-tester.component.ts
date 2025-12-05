import { Component, signal } from '@angular/core';
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

@Component({
  selector: 'app-webhook-tester',
  imports: [CommonModule, FormsModule],
  templateUrl: './webhook-tester.component.html',
  styleUrl: './webhook-tester.component.scss'
})
export class WebhookTesterComponent {
  url = signal('http://192.168.1.22:80/');
  statusCode = signal('404');
  apiEndpoint = signal('http://localhost:5250/webhook/incident');

  isLoading = signal(false);
  response = signal<WebhookResponse | null>(null);
  error = signal<string | null>(null);

  constructor(private http: HttpClient) {}

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
        },
        error: (err) => {
          this.error.set(err.message || 'An error occurred');
          this.isLoading.set(false);
        }
      });
  }

  resetForm(): void {
    this.url.set('http://192.168.1.22:80/');
    this.statusCode.set('404');
    this.response.set(null);
    this.error.set(null);
  }
}
