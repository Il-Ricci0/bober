import { Routes } from '@angular/router';
import { AppLayout } from './layout/component/app.layout';
import { IncidentsComponent } from './pages/incidents/incidents.component';

export const appRoutes: Routes = [
    {
        path: '',
        component: AppLayout,
        children: [
            { path: '', component: IncidentsComponent },
            { path: 'incidents', component: IncidentsComponent }
        ]
    },
    { path: '**', redirectTo: '/' }
];
