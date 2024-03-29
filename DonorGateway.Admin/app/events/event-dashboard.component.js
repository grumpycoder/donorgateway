﻿//event-dashboard.component.js
(function () {
    var module = angular.module('app');

    function eventDashboardController($http, $modal, toastr) {
        var ctrl = this;

        ctrl.title = 'Event Manager';
        ctrl.description = "Manage Donor Events";

        ctrl.$onInit = function () {
            console.log('event dashboard init');
            ctrl.isBusy = true; 
            $http.get('api/event').then(function (r) {
                ctrl.events = r.data;
            }).catch(function (err) {
                console.log("Oops. Can't get list of events", err);
                toastr.error("Oops. Can't get list of events");
            }).finally(function () {
                ctrl.isBusy = false;
            });
        }

        ctrl.create = function() {
            $modal.open({
                component: 'eventCreate',
                bindings: {
                    modalInstance: "<"
                },
                size: 'md'
            }).result.then(function (result) {
                ctrl.selectedEvent = result;
                ctrl.events.unshift(ctrl.selectedEvent);
                console.log('selected event', ctrl.selectedEvent);
                toastr.info('Created Event ' + result.name);
            }, function (reason) {
            });            
        }

        ctrl.changeEvent = function() {
            ctrl.selectedEventId = ctrl.selectedEvent.id; 
        }

        ctrl.eventDeleted = function() {
            var idx = ctrl.events.indexOf(ctrl.selectedEvent);
            ctrl.events.splice(idx, 1);
            ctrl.selectedEvent = null; 
        }

        ctrl.eventUpdated = function (event) {
            ctrl.isBusy = true; 
            angular.extend(ctrl.selectedEvent, event);
            ctrl.isBusy = false; 
        }
    }

    module.component('eventDashboard',
        {
            bindings: {
            },
            templateUrl: 'app/events/event-dashboard.component.html',
            controller: ['$http', '$uibModal', 'toastr', eventDashboardController]
        });
}
)();