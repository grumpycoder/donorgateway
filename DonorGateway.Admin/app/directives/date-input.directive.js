﻿//mark.lawrence
//date-input.directive.js

(function () {
    angular.module('app').directive('dateInput', function ($window) {
        return {
            require: '^ngModel',
            restrict: 'A',
            link: function (scope, elm, attrs, ctrl) {
                var moment = $window.moment;
                var dateFormat = attrs.dateInput;

                attrs.$observe('dateInput', function (newValue) {
                    if (dateFormat === newValue || !ctrl.$modelValue) return;

                    dateFormat = newValue;
                    ctrl.$modelValue = moment(ctrl.$setViewValue).format(dateFormat);
                });

                ctrl.$formatters.unshift(function (modelValue) {
                    if (!dateFormat || !modelValue) return "";
                    var retVal = moment(modelValue).format(dateFormat);
                    return retVal;
                });

                ctrl.$parsers.unshift(function (viewValue) {
                    var date = moment(viewValue, dateFormat);
                    return (date && date.isValid() && date.year() > 1950) ? date.toDate() : "";
                });
            }
        };
    });
})();