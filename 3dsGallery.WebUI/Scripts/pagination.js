(function ($) {

    $.fn.pagination = function (pageUrl, totalPages, options) {
                
        var thisElement = this;
        var defaultSettings = getDefaultSettings();
        var settings = $.extend({}, defaultSettings, options);
        var style = getDefaultStyles();

        style[settings.paginationStyle].create(settings.currentPage, totalPages);



        /**
        * @summary Повертає об'єкт зі стандартними налаштуваннями.
        */
        function getDefaultSettings() {
            return {
                currentPage: 1,
                urlParameters: {},                  // Додаткові параметри сторінки, яка завантажується.
                dontLoadActiveOrDisabledPage: true, // true - не завантажує сторінки з класами "active" чи "disabled".
                paginationStyle: "allPages",        // "allPages", "allPagesShrink".
                paginationStyleFlexible: false,     // true - кількість сторінок підлаштовується під розмір контейнеру.
                visiblePagesCount: 5,               // Кількість сторінок, які відображаються (стилі: "allPagesShrink").
            };
        }

        /**
        * @summary Повертає об'єкт зі стандартними стилями.
        */
        function getDefaultStyles() {
            return {

                /**
                * @summary Стиль відображає всі існуючі сторінки без додаткових кнопок.
                */
                "allPages": {

                    /**
                     * @summary Створення елементів "пейджингу" (ul.pagination).
                     *
                     * @param {number} currentPage Відображається сторінка з цим номером.
                     * @param {number} totalPages  Загальна кількість сторінок.
                     */
                    "create": function (currentPage, totalPages) {

                        $(thisElement).empty();
                        var container = $("<ul/>", { class: "pagination" }).css("display", "block");
                        var pageElement = $("<a/>", { class: "page", 'data-page': 1 });

                        for (var i = 1; i <= totalPages; i++) {
                            var params = $.extend({ page: i }, settings.urlParameters);
                            var clonedPageElement = pageElement.clone(true).attr({ "data-page": i, href: pageUrl + "?" + $.param(params) }).text(i);
                            container.append($("<li/>").append(clonedPageElement));
                        }

                        container.find("a[data-page='" + currentPage + "']").click(function (e) { e.preventDefault(); }).parent("li").addClass("active");
                        $(thisElement).append(container);
                    }
                },
                /**
                * @summary Стиль відображає всі існуючі сторінки, ховаючи деякі з них в "dropdown".
                *
                * @description  Відображаються кнопки "вперед"/"назад" при необхідності;
                                4 найближчих сторінки;
                                сторінки, які не помістились знаходяться в "dropdown" (клікабельні кнопки "...").
                */
                "allPagesShrink": {

                    /**
                     * @summary Створення елементів "пейджингу" (ul.pagination).
                     *
                     * @param {number} currentPage Відображається сторінка з цим номером.
                     * @param {number} totalPages  Загальна кількість сторінок.
                     */
                    "create": function (currentPage, totalPages) {

                        $(thisElement).empty();
                        if (totalPages < 2) {
                            return false;
                        }

                        // Шаблони елементів та контейнер:
                        var container = $("<ul/>", { class: "pagination" }).css("display", "block");
                        var pageElement = $("<a/>", { class: "page", 'data-page': 1 });
                        var dropdown = $("<li/>")
                            .append($("<div/>", { class: "dropup btn-group", style: "display:block" })
                                .append($("<button/>", { class: "btn dropdown-toggle", type: "button", 'data-toggle': "dropdown", text: "..." })));

                        // Данні по сторінкам:
                        var visiblePages = settings.visiblePagesCount;
                        var visiblePagesLeft = totalPages;
                        var visiblePagesRight = totalPages;

                        if (visiblePages >= totalPages) {
                            visiblePages = totalPages;
                        } else {
                            if (visiblePages % 2 == 0) {
                                visiblePagesLeft = visiblePages / 2 - 1;
                                visiblePagesRight = visiblePages / 2;
                            } else {
                                visiblePagesLeft = visiblePagesRight = parseInt(visiblePages / 2);
                            }
                        }
                        var pageFrom = (currentPage - visiblePagesLeft > 0) ? currentPage - visiblePagesLeft : 1;
                        var pageTo = (currentPage + visiblePagesRight < totalPages) ? currentPage + visiblePagesRight : totalPages;
                        if (visiblePages < totalPages) {
                            pageTo = (currentPage - pageFrom < visiblePagesLeft) ? pageTo + (visiblePagesLeft - currentPage + 1) : pageTo;
                            pageFrom = (pageTo - currentPage < visiblePagesRight) ? pageFrom - (visiblePagesRight - (pageTo - currentPage)) : pageFrom;
                        }

                        // Якщо обрана сторінка не перша - створю кнопку "назад"
                        if (currentPage > 1) {
                            var params = $.extend({ page: currentPage - 1 }, settings.urlParameters);
                            $("<li/>")
                                .append(pageElement.clone(true).attr({ "data-page": currentPage - 1, href: pageUrl + "?" + $.param(params) }).addClass("page-nav page-prev").text("<"))
                                .appendTo(container);
                        }

                        // Створюю дропдаун для попередніх сторінок, яких забагато
                        createDropdown(1, pageFrom);

                        // Створюю найближчі видимі кнопки сторінок
                        for (var i = pageFrom; i <= pageTo; i++) {
                            var params = $.extend({ page: i }, settings.urlParameters);
                            $("<li/>")
                                .append(pageElement.clone(true).attr({ "data-page": i, href: pageUrl + "?" + $.param(params) }).text(i))
                                .appendTo(container);
                        }

                        // Створюю дропдаун для наступних сторінок, яких забагато
                        createDropdown(pageTo + 1, totalPages + 1);

                        // Якщо обрана сторінка не остання - створюю кнопку "вперед"
                        if (currentPage < totalPages) {
                            var params = $.extend({ page: currentPage + 1 }, settings.urlParameters);
                            $("<li/>")
                                .append(pageElement.clone(true).attr({ "data-page": currentPage + 1, href: pageUrl + "?" + $.param(params) }).addClass("page-nav page-next").text(">"))
                                .appendTo(container);
                        }

                        // Виділяю обрану сторінку як активну та додаю контейнер в елемент пейджингу
                        container.find("a[data-page='" + currentPage + "']").click(function (e) { e.preventDefault(); }).parent("li").addClass("active");
                        container.appendTo(thisElement);

                        /**
                         * @summary Створює дродаун зі сторінками. 
                         *
                         * @param {number} from Починаючи з цієї сторінки.
                         * @param {number} to   Закінчуючи цією сторінкою не включно (i < to)
                         */
                        function createDropdown(from, to) {
                            var hiddenElementsList = $("<ul/>", { class: "dropdown-menu" });
                            for (var i = from; i < to; i++) {
                                var params = $.extend({ page: i }, settings.urlParameters);
                                hiddenElementsList
                                    .append($("<li/>")
                                        .append(pageElement.clone(true).attr({ "data-page": i, href: pageUrl + "?" + $.param(params) }).text(i)));
                            }

                            var clonedDropdown = dropdown.clone(true);
                            clonedDropdown.appendTo(container).find("div.dropup").append(hiddenElementsList);
                            if (!clonedDropdown.find(hiddenElementsList).children("li").length) {
                                clonedDropdown.hide();
                            }

                        }
                    }
                }
            };
        }
    }
}(jQuery));