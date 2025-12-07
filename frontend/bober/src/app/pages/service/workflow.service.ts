import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MonitorEvent, WebhookResponse, WorkflowStatus } from '../../models/workflow.model';

@Injectable({
    providedIn: 'root'
})
export class WorkflowService {
    private apiUrl = 'http://localhost:5250';

    constructor(private http: HttpClient) {}

    /**
     * Get all workflows
     */
    getWorkflows(): Observable<WorkflowStatus[]> {
        return this.http.get<WorkflowStatus[]>(`${this.apiUrl}/workflows`);
    }

    /**
     * Create a new incident by sending a webhook
     */
    createIncident(event: MonitorEvent): Observable<WebhookResponse> {
        return this.http.post<WebhookResponse>(`${this.apiUrl}/webhook/incident`, event);
    }

    /**
     * Cancel a running workflow
     */
    cancelWorkflow(incidentId: string): Observable<void> {
        return this.http.post<void>(`${this.apiUrl}/workflows/${incidentId}/cancel`, {});
    }
}
