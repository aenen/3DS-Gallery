        function createPagination(currentPage, totalPages) {
            var thisElement = $("#pagination");
            $(thisElement).empty();
            if (totalPages < 2) {
                return false;
            }

            // Шаблони елементів та контейнер:
            var container = $("<ul/>").addClass("pagination").css("display", "block");
            var pageElement = $("<a/>").addClass("page").attr('data-page', 1);
            var dropdown = $("<li/>")
                .append($("<div/>").addClass("dropup btn-group").css("display", "block")
                .append($("<button/>").addClass("btn dropdown-toggle").attr({ type: "button", 'data-toggle': "dropdown" }).text("..." )));

            // Данні по сторінкам:
            var visiblePages = 3;
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
                var params = $.extend({ page: currentPage - 1 }, {
                        'filter': '@ViewBag.Filter',
                    });
                $("<li/>")
                    .append(pageElement.clone(true).attr({ "data-page": currentPage - 1, href: '@Url.Action("Details", "Gallery")' + "?" + $.param(params) }).addClass("page-nav page-prev").text("<"))
                    .appendTo(container);
            }

            // Створюю дропдаун для попередніх сторінок, яких забагато
            createDropdown(1, pageFrom);

            // Створюю найближчі видимі кнопки сторінок
            for (var i = pageFrom; i <= pageTo; i++) {
                var params = $.extend({ page: i }, {
                        'filter': '@ViewBag.Filter',
                    });
                $("<li/>")
                    .append(pageElement.clone(true).attr({ "data-page": i, href: '@Url.Action("Details", "Gallery")' + "?" + $.param(params) }).text(i))
                    .appendTo(container);
            }

            // Створюю дропдаун для наступних сторінок, яких забагато
            createDropdown(pageTo + 1, totalPages + 1);

            // Якщо обрана сторінка не остання - створюю кнопку "вперед"
            if (currentPage < totalPages) {
                var params = $.extend({ page: currentPage + 1 }, {
                        'filter': '@ViewBag.Filter',
                    });
                $("<li/>")
                    .append(pageElement.clone(true).attr({ "data-page": currentPage + 1, href: '@Url.Action("Details", "Gallery")' + "?" + $.param(params) }).addClass("page-nav page-next").text(">"))
                    .appendTo(container);
            }

            // Виділяю обрану сторінку як активну та додаю контейнер в елемент пейджингу
            container.find("a[data-page='" + currentPage + "']").click(function (e) { e.preventDefault(); }).parent("li").addClass("active");
            container.appendTo(thisElement);

            function createDropdown(from, to) {
                var hiddenElementsList = $("<ul/>").addClass( "dropdown-menu" );
                for (var i = from; i < to; i++) {
                    var params = $.extend({ page: i }, {
                        'filter': '@ViewBag.Filter',
                    });
                    hiddenElementsList
                        .append($("<li/>")
                            .append(pageElement.clone(true).attr({ "data-page": i, href: '@Url.Action("Details", "Gallery")' + "?" + $.param(params) }).text(i)));
                }

                var clonedDropdown = dropdown.clone(true);
                clonedDropdown.appendTo(container).find("div.dropup").append(hiddenElementsList);
                if (!clonedDropdown.find(hiddenElementsList).children("li").length) {
                    clonedDropdown.hide();
                }

            }
        }
