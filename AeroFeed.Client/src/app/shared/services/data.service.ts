import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable } from 'rxjs';

@Injectable({ providedIn: 'root' }) // This makes it a Singleton
export class KafkaDataService {
  private hubConnection: signalR.HubConnection;
  
  // A Subject is like a private 'source' of data
  private dataSubject = new BehaviorSubject<any>(null);
  
  // Components 'subscribe' to this public Observable
  public currentData$ = this.dataSubject.asObservable();

  constructor() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/notificationHub')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveUpdate', (data) => {
      this.dataSubject.next(data); // Push new Kafka data into the stream
      console.log(data)
    });

    this.hubConnection.start();
  }
}