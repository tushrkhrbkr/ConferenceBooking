/*
-------------------------------------------------------------------------
* Template Name    : DashHub - Tailwind CSS Admin & Dashboard Template  * 
* Author           : Webonzer                                           *
* Version          : 1.0.0                                              *
* Created          : June 2023                                          *
* File Description : Main Js file of the template                       *
*------------------------------------------------------------------------
*/
var userchart = {
    series: [
        {
            data: [12, 14, 2, 47, 42, 15, 47, 75, 65, 19, 14],
        },
    ],
    chart: {
        height: 80,
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
        height: 80,
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
