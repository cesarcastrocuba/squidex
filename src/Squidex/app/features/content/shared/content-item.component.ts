/*
 * Squidex Headless CMS
 *
 * @license
 * Copyright (c) Sebastian Stehle. All rights reserved
 */

import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';

import {
    AppComponentBase,
    AppLanguageDto,
    AppsStoreService,
    ContentDto,
    fadeAnimation,
    FieldDto,
    ModalView,
    NotificationService,
    SchemaDto
} from 'shared';

@Component({
    selector: '[sqxContent]',
    styleUrls: ['./content-item.component.scss'],
    templateUrl: './content-item.component.html',
    animations: [
        fadeAnimation
    ]
})
export class ContentItemComponent extends AppComponentBase implements OnInit, OnChanges {
    public dropdown = new ModalView(false, true);

    @Output()
    public publishing = new EventEmitter<ContentDto>();

    @Output()
    public unpublishing = new EventEmitter<ContentDto>();

    @Output()
    public deleting = new EventEmitter<ContentDto>();

    @Input()
    public language: AppLanguageDto;

    @Input()
    public schemaFields: FieldDto[];

    @Input()
    public schema: SchemaDto;

    @Input()
    public isReadOnly = false;

    @Input()
    public isReference = false;

    @Input('sqxContent')
    public content: ContentDto;

    public values: any[] = [];

    constructor(apps: AppsStoreService, notifications: NotificationService) {
        super(notifications, apps);
    }

    public ngOnChanges() {
        this.updateValues();
    }

    public ngOnInit() {
        this.updateValues();
    }

    private updateValues() {
        this.values = [];

        if (this.schemaFields) {
            for (let field of this.schemaFields) {
                this.values.push(this.getValue(field));
            }
        }
    }

    private getValue(field: FieldDto): any {
        const contentField = this.content.data[field.name];

        if (contentField) {
            if (field.partitioning === 'language') {
                return field.formatValue(contentField[this.language.iso2Code]);
            } else {
                return field.formatValue(contentField['iv']);
            }
        } else {
            return '';
        }
    }
}

