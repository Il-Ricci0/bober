/**
 * Models for workflow and incident management
 */

export interface MonitorEvent {
    Url: string;
    StatusCode: string;
}

export interface WebhookResponse {
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

export enum WorkflowState {
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

export interface WorkflowStatus {
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
