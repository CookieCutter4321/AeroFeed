
import { Component } from '@angular/core';
import {
  ApexNonAxisChartSeries,
  ApexChart,
  ApexPlotOptions,
  ApexFill,
  ApexStroke,
  NgApexchartsModule,
} from 'ng-apexcharts';
import { DropdownComponent } from '../../ui/dropdown/dropdown.component';
import { DropdownItemComponent } from '../../ui/dropdown/dropdown-item/dropdown-item.component';
import { KafkaDataService } from '../../../services/data.service';
import { PI } from '@amcharts/amcharts5/.internal/core/util/Math';
import { AsyncPipe, DecimalPipe } from '@angular/common';

@Component({
  selector: 'app-monthly-target',
  imports: [
    NgApexchartsModule,
    DropdownComponent,
    DropdownItemComponent,
    AsyncPipe,
    DecimalPipe
],
  templateUrl: './monthly-target.component.html',
})
export class MonthlyTargetComponent {
  public series: ApexNonAxisChartSeries = [NaN];
  public chart: ApexChart = {
    fontFamily: 'Outfit, sans-serif',
    type: 'radialBar',
    height: 330,
    sparkline: { enabled: true },
  };

  constructor(public kafkaService : KafkaDataService) {
  }
  
  ngAfterViewInit(): void {
    this.kafkaService.currentData$.subscribe((data) => {
      if (data == null || data.bots == null || data.nonBots == null) return;
      const average: number = Math.round(((data.bots)/(data.bots + data.nonBots) * 100) * 100) / 100;
      this.series = [average];
    })
  }

  public plotOptions: ApexPlotOptions = {
    radialBar: {
      startAngle: -85,
      endAngle: 85,
      hollow: { size: '80%' },
      track: {
        background: '#E4E7EC',
        strokeWidth: '100%',
        margin: 5,
      },
      dataLabels: {
        name: { show: false },
        value: {
          fontSize: '36px',
          fontWeight: '600',
          offsetY: -40,
          color: '#1D2939',
          formatter: (val: number) => `${val}%`,
        },
      },
    },
  };
  public fill: ApexFill = {
    type: 'solid',
    colors: ['#465FFF'],
  };
  public stroke: ApexStroke = {
    lineCap: 'round',
  };
  public labels: string[] = ['Progress'];
  public colors: string[] = ['#465FFF'];

  isOpen = false;

  toggleDropdown() {
    this.isOpen = !this.isOpen;
  }

  closeDropdown() {
    this.isOpen = false;
  }
}
