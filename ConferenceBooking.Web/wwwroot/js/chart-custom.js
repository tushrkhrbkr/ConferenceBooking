/*
-------------------------------------------------------------------------
* Template Name    : DashHub - Tailwind CSS Admin & Dashboard Template  * 
* Author           : Webonzer                                           *
* Version          : 1.0.0                                              *
* Created          : June 2023                                          *
* File Description : Main Js file of the template                       *
*------------------------------------------------------------------------
*/
// Chart Widget 5
var userchart = {
    chart: {
        height: 300,
        type: "area",
        fontFamily: "Inter, sans-serif",
        zoom: {
            enabled: false,
        },
        toolbar: {
            show: false,
        },
    },
    series: [
        {
            name: "Income",
            data: [8000, 1000, 7600, 2000, 15000, 7000, 15000],
        },
        {
            name: "Expenses",
            data: [7000, 1300, 9000, 1000, 12000, 6000, 14000],
        },
    ],
    dataLabels: {
        enabled: false,
    },
    stroke: {
        show: true,
        curve: "smooth",
        width: 3,
        lineCap: "square",
    },
    dropShadow: {
        enabled: false,
    },
    colors: ["#267DFF", "#00D085"],
    markers: {
        discrete: [
            {
                seriesIndex: 0,
                dataPointIndex: 4,
                fillColor: "#267DFF",
                strokeColor: "#fff",
                size: 6,
            },
            {
                seriesIndex: 1,
                dataPointIndex: 5,
                fillColor: "#00D085",
                strokeColor: "#fff",
                size: 6,
            },
        ],
    },
    labels: [
        "Jan 10",
        "Jan 11",
        "Jan 12",
        "Jan 13",
        "Jan 14",
        "Jan 15",
        "Jan 16",
    ],
    xaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        crosshairs: {
            show: true,
        },
        labels: {
            offsetX: 0,
            offsetY: 5,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
    },
    yaxis: {
        tickAmount: 7,
        labels: {
            formatter: (value) => {
                return value / 1000 + "k";
            },
            offsetX: -10,
            offsetY: 0,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-yaxis-title",
            },
        },
        opposite: false,
    },
    grid: {
        borderColor: "#e0e6ed",
        strokeDashArray: 7,
        xaxis: {
            lines: {
                show: true,
            },
        },
        yaxis: {
            lines: {
                show: false,
            },
        },
        padding: {
            top: 0,
            right: 0,
            bottom: 0,
            left: 25,
        },
    },
    legend: {
        position: "top",
        horizontalAlign: "center",
    },
    tooltip: {
        marker: {
            show: true,
        },
        x: {
            show: false,
        },
    },
    fill: {
        type: "gradient",
        gradient: {
            shadeIntensity: 1,
            inverseColors: !1,
            opacityFrom: 0,
            opacityTo: 0,
            stops: [100, 100],
        },
    },
};
var chart = new ApexCharts(document.querySelector("#capital"), userchart);
chart.render();
var userchart = {
    chart: {
        height: 300,
        type: "bar",
        fontFamily: "Inter, sans-serif",
        zoom: {
            enabled: false,
        },
        toolbar: {
            show: false,
        },
    },
    plotOptions: {
        bar: {
            horizontal: !1,
            endingShape: "rounded",
            columnWidth: "50%",
            borderRadius: 5,
        },
    },
    stroke: {
        show: !0,
        width: 4,
        colors: ["transparent"],
    },
    colors: ["rgba(123, 106, 254, .4)", "#7B6AFE"],
    series: [
        {
            name: "Income",
            data: [8000, 5000, 7600, 2200, 15000, 7000, 15000],
        },
        {
            name: "Expenses",
            data: [7000, 4500, 9000, 1600, 12000, 6000, 14000],
        },
    ],
    dataLabels: {
        enabled: false,
    },
    legend: {
        show: false,
    },
    labels: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
    xaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        crosshairs: {
            show: true,
        },
        labels: {
            offsetX: 0,
            offsetY: 5,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
    },
    yaxis: {
        show: false,
    },
    fill: { opacity: 1 },
    grid: {
        borderColor: "#e0e6ed",
        strokeDashArray: 7,
        xaxis: {
            lines: {
                show: false,
            },
        },
        yaxis: {
            lines: {
                show: false,
            },
        },
        padding: {
            top: 0,
            right: 0,
            bottom: 0,
            left: 0,
        },
    },
    tooltip: {
        y: {
            formatter: function (e) {
                return "" + e;
            },
        },
    },
};
var chart = new ApexCharts(
    document.querySelector("#projectvsaction"),
    userchart
);
chart.render();
var userchart = {
    series: [
        {
            name: "Device",
            data: [200, 230, 400, 500, 200, 300, 140],
        },
    ],
    chart: {
        height: 280,
        type: "bar",
        events: {
            click: function (chart, w, e) {},
        },
        toolbar: {
            show: false,
        },
        fontFamily: "Inter, sans-serif",
        toolbar: {
            show: false,
        },
    },
    colors: [
        "#267DFF",
        "#7B6AFE",
        "#FF51A4",
        "#FF7C51",
        "#00D085",
        "#FFC41F",
        "#FF3232",
    ],
    plotOptions: {
        bar: {
            columnWidth: "20%",
            distributed: true,
            borderRadius: 5,
        },
    },
    dataLabels: {
        enabled: false,
    },
    legend: {
        show: false,
    },
    yaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        tickAmount: 6,
        labels: {
            formatter: (value) => {
                return value / 10 + "K";
            },
            offsetX: -10,

            offsetY: 0,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
        opposite: false,
    },
    xaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        categories: [
            "Phones",
            "Laptops",
            "Headsets",
            "Keyboards",
            "Clothes",
            "Speakers",
            "Air buds",
        ],
        labels: {
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
    },
    grid: {
        borderColor: "#e0e6ed",
        strokeDashArray: 2,
        xaxis: {
            lines: {
                show: false,
            },
        },
        yaxis: {
            lines: {
                show: true,
            },
        },
        padding: {
            top: 0,
            right: 0,
            bottom: 0,
            left: 25,
        },
    },
};
var chart = new ApexCharts(document.querySelector("#topsell"), userchart);
chart.render();
var userchart = {
    chart: {
        height: 280,
        type: "bar",
        fontFamily: "Inter, sans-serif",
        zoom: {
            enabled: false,
        },
        toolbar: {
            show: false,
        },
    },
    plotOptions: {
        bar: {
            horizontal: !1,
            endingShape: "rounded",
            columnWidth: "30%",
            borderRadius: 2,
        },
    },
    stroke: {
        show: !0,
        width: 4,
        colors: ["transparent"],
    },
    colors: ["#267DFF", "rgba(201, 223, 255)"],
    series: [
        {
            name: "Income",
            data: [62, 81, 76, 22, 45, 70, 15],
        },
        {
            name: "Expenses",
            data: [55, 70, 56, 42, 36, 60, 14],
        },
    ],
    dataLabels: {
        enabled: false,
    },
    legend: {
        show: false,
    },
    labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul"],
    xaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        crosshairs: {
            show: true,
        },
        labels: {
            offsetX: 0,
            offsetY: 3,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
    },
    yaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        crosshairs: {
            show: true,
        },
        labels: {
            offsetX: 0,
            offsetY: 9,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
    },
    fill: { opacity: 1 },
    grid: {
        borderColor: "rgba(119, 128, 161, .1)",
        strokeDashArray: 1,
        xaxis: {
            lines: {
                show: false,
            },
        },
        yaxis: {
            lines: {
                show: true,
            },
        },
        padding: {
            top: 0,
            right: 0,
            bottom: 0,
            left: 0,
        },
    },
    tooltip: {
        y: {
            formatter: function (e) {
                return "" + e;
            },
        },
    },
};
var chart = new ApexCharts(document.querySelector("#topselling"), userchart);
chart.render();
var options = {
    series: [
        {
            name: "Income",
            data: [8000, 1000, 7600, 2000, 11000, 7000, 15000],
        },
    ],
    chart: {
        height: 270,
        type: "line",
        dropShadow: {
            enabled: true,
            blur: 5,
            color: "#000",
            opacity: 0.25,
        },
        fontFamily: "Inter, sans-serif",
        zoom: {
            enabled: false,
        },
        toolbar: {
            show: false,
        },
    },

    stroke: {
        width: 5,
        curve: "smooth",
    },
    dataLabels: {
        enabled: false,
    },
    fill: {
        type: "gradient",
        gradient: {
            shade: "dark",
            gradientToColors: ["#FF51A4"],
            shadeIntensity: 1,
            type: "horizontal",
            opacityFrom: 1,
            opacityTo: 1,
            stops: [0, 48, 54, 122],
        },
    },
    labels: ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
    xaxis: {
        axisBorder: {
            show: false,
        },
        axisTicks: {
            show: false,
        },
        crosshairs: {
            show: true,
        },
        labels: {
            offsetX: 0,
            offsetY: 5,
            style: {
                fontSize: "16px",
                fontWeight: "600",
                colors: "#7780A1",
                cssClass: "apexcharts-xaxis-title",
            },
        },
    },
    yaxis: {
        show: false,
    },
    grid: {
        borderColor: "#e0e6ed",
        strokeDashArray: 2,
        xaxis: {
            lines: {
                show: true,
            },
        },
        yaxis: {
            lines: {
                show: false,
            },
        },
        padding: {
            top: 0,
            right: 0,
            bottom: 0,
            left: 25,
        },
    },
    legend: {
        show: false,
    },
    tooltip: {
        marker: {
            show: false,
        },
        x: {
            show: false,
        },
    },
};

var chart = new ApexCharts(document.querySelector("#linechart"), options);
chart.render();

var userchart = {
    series: [
        {
            data: [12, 14, 2, 47, 42, 15, 47, 75, 65, 19, 14],
        },
    ],
    chart: {
        type: "bar",
        sparkline: {
            enabled: true,
        },
    },
    colors: ["#267DFF"],
    plotOptions: {
        bar: {
            columnWidth: "60%",
        },
    },
    labels: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
    xaxis: {
        crosshairs: {
            width: 1,
        },
    },
    tooltip: {
        fixed: {
            enabled: false,
        },
        x: {
            show: false,
        },
        y: {
            title: {
                formatter: function (seriesName) {
                    return "";
                },
            },
        },
        marker: {
            show: false,
        },
    },
};
var chart = new ApexCharts(document.querySelector("#visitors"), userchart);
chart.render();

var userchart = {
    series: [
        {
            data: [19, 8, 7, 35, 12, 23, 34, 65, 36, 33, 14],
        },
    ],
    chart: {
        type: "bar",
        sparkline: {
            enabled: true,
        },
    },
    colors: ["#7B6AFE"],
    plotOptions: {
        bar: {
            columnWidth: "60%",
        },
    },
    labels: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
    xaxis: {
        crosshairs: {
            width: 1,
        },
    },
    tooltip: {
        fixed: {
            enabled: false,
        },
        x: {
            show: false,
        },
        y: {
            title: {
                formatter: function (seriesName) {
                    return "";
                },
            },
        },
        marker: {
            show: false,
        },
    },
};
var chart = new ApexCharts(document.querySelector("#direct"), userchart);
chart.render();

var userchart = {
    series: [
        {
            data: [5, 20, 35, 45],
        },
    ],
    chart: {
        height: 90,
        type: "line",
        sparkline: {
            enabled: true,
        },
    },
    stroke: {
        width: 2,
        curve: "stepline",
    },
    colors: ["#267DFF"],
    plotOptions: {
        bar: {
            columnWidth: "60%",
        },
    },
    labels: [1, 2, 3, 4],
    xaxis: {
        crosshairs: {
            width: 1,
        },
    },
    tooltip: {
        fixed: {
            enabled: false,
        },
        x: {
            show: false,
        },
        y: {
            title: {
                formatter: function (seriesName) {
                    return "";
                },
            },
        },
        marker: {
            show: false,
        },
    },
};
var chart = new ApexCharts(document.querySelector("#photoclick"), userchart);
chart.render();

var userchart = {
    series: [
        {
            data: [5, 20, 35, 45],
        },
    ],
    chart: {
        height: 90,
        type: "line",
        sparkline: {
            enabled: true,
        },
    },
    stroke: {
        width: 2,
        curve: "stepline",
    },
    colors: ["#7B6AFE"],
    plotOptions: {
        bar: {
            columnWidth: "60%",
        },
    },
    labels: [1, 2, 3, 4],
    xaxis: {
        crosshairs: {
            width: 1,
        },
    },
    tooltip: {
        fixed: {
            enabled: false,
        },
        x: {
            show: false,
        },
        y: {
            title: {
                formatter: function (seriesName) {
                    return "";
                },
            },
        },
        marker: {
            show: false,
        },
    },
};
var chart = new ApexCharts(document.querySelector("#linkclick"), userchart);
chart.render();