import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, animationFrameScheduler, observeOn, auditTime, sampleTime } from 'rxjs';

@Injectable({ providedIn: 'root' }) // This makes it a Singleton
export class KafkaDataService {
  private hubConnection: signalR.HubConnection;
  
  // A Subject is like a private 'source' of data
  private dataSubject = new BehaviorSubject<any>(null);
  
  // Components 'subscribe' to this public Observable
  public currentData$ = this.dataSubject.asObservable().pipe(
    sampleTime(100), 
    observeOn(animationFrameScheduler)
  );
  constructor() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/notificationHub')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveUpdate', (data) => {
      let dateTime = new Date();
      console.log(dateTime);
      console.log(data.netLength);
      this.dataSubject.next(data);
    });

    this.hubConnection.start();
  }
}